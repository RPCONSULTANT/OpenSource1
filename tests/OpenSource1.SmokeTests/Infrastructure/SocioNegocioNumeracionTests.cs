using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenSource1.Application.Data;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.SociosNegocio.Commands;
using OpenSource1.Application.Features.SociosNegocio.Handlers;
using OpenSource1.Core.Abstractions;
using OpenSource1.Core.Enums;
using OpenSource1.Infrastructure.Data;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Infrastructure;

/// <summary>
/// Primer consumidor real de la numeración (Task 2.2): el alta de socio de negocio reserva el
/// código de la serie SOCIOS dentro de una transacción. Estas pruebas cubren el caso que la vía
/// HTTP no puede forzar de forma determinista: una EXCEPCIÓN (no un <c>Result</c> fallido) después
/// de reservar el número y de haber ejecutado el INSERT. El rollback debe devolver el número y
/// descartar la fila. REQUIERE DOCKER (mismo esquema que <see cref="GeneradorNumeroDocumentoTests"/>).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SocioNegocioNumeracionTests : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private readonly PostgresTestFixture _fixture;
    private ServiceProvider _provider = null!;

    public SocioNegocioNumeracionTests(PostgresTestFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _fixture.AppConnectionString,
                ["ConnectionStrings:IdentityConnection"] = _fixture.IdentityConnectionString,
                ["Database:ApplyMigrationsOnStartup"] = "false",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddApplicationData(configuration);
        _provider = services.BuildServiceProvider();

        var migrationOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_fixture.AppConnectionString).Options;
        await using var migrationContext = new ApplicationDbContext(migrationOptions);
        await migrationContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    [Fact]
    public async Task Alta_ConExcepcionTrasReservarEInsertar_NoConsumeNumeroNiDejaLaFila()
    {
        // La migración sembró la serie SOCIOS con el contador en 0 (base sin filas).
        var primero = await AltaAsync("Primero", falloForzado: false);
        Assert.Equal("000001", primero);

        // Alta que revienta DESPUÉS de reservar el número y de ejecutar el INSERT (SaveChanges),
        // justo antes de confirmar: excepción forzada.
        await Assert.ThrowsAsync<InvalidOperationException>(() => AltaAsync("Con fallo forzado", falloForzado: true));

        var siguiente = await AltaAsync("Despues del fallo", falloForzado: false);

        // El fallo no consumió el 000002: el siguiente alta lo recibe, sin hueco.
        Assert.Equal("000002", siguiente);

        var (ultimo, filas) = await LeerContadorAsync();
        Assert.Equal("2", ultimo.TrimStart('0'));
        Assert.Equal(2, filas);
    }

    private async Task<string> AltaAsync(string nombre, bool falloForzado)
    {
        await using var scope = _provider.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var generador = scope.ServiceProvider.GetRequiredService<IGeneradorNumeroDocumento>();

        IUnitOfWork efectivo = falloForzado ? new FalloAlConfirmarUnitOfWork(unitOfWork) : unitOfWork;
        var handler = new CreateSocioNegocioCommandHandler(efectivo, generador);

        var resultado = await handler.Handle(
            new CreateSocioNegocioCommand(
                nombre, null, TipoSocioNegocio.Cliente, TipoDocumentoFiscal.SinDocumento, null, null, null,
                null, null, null, null, null, null, 0m, BloqueoSocioNegocio.Ninguno),
            default);

        Assert.True(resultado.EsExito, string.Join("; ", resultado.Errores.Select(e => e.Codigo)));
        return resultado.Valor.Codigo;
    }

    private async Task<(string Ultimo, long Filas)> LeerContadorAsync()
    {
        await using var conexion = new NpgsqlConnection(_fixture.AppConnectionString);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(
            """
            SELECT l."UltimoNumeroUsado", (SELECT COUNT(*) FROM "SociosNegocio")
            FROM "LineasSerie" l JOIN "Series" s ON s."Id" = l."SerieId" WHERE s."Codigo" = 'SOCIOS'
            """, conexion);
        await using var lector = await comando.ExecuteReaderAsync();
        Assert.True(await lector.ReadAsync());
        return (lector.GetString(0), lector.GetInt64(1));
    }

    /// <summary>
    /// Decorador de prueba: ejecuta el SaveChanges real (el INSERT llega a la base dentro de la
    /// transacción) y luego lanza, simulando un fallo entre el INSERT y el commit.
    /// </summary>
    private sealed class FalloAlConfirmarUnitOfWork(IUnitOfWork interno) : IUnitOfWork
    {
        public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class, IAggregateRoot => interno.Repository<TEntity>();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => interno.SaveChangesAsync(cancellationToken);
        public bool HayTransaccionActiva => interno.HayTransaccionActiva;
        public Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default) => interno.BeginTransactionAsync(cancellationToken);
        public Task RollbackAsync(CancellationToken cancellationToken = default) => interno.RollbackAsync(cancellationToken);

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await interno.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Fallo forzado entre el INSERT y el commit.");
        }
    }
}
