using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenSource1.Application.Data;
using OpenSource1.Infrastructure.Data;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Infrastructure;

/// <summary>
/// Prueba de la propiedad central de <see cref="IDbSession"/>: EF Core y Dapper comparten la
/// misma conexión física y transacción, de modo que una escritura EF sin commit es visible
/// para una lectura Dapper en el mismo scope, y deja de serlo tras el rollback. Es la propiedad
/// que las conexiones separadas de antes (<c>IDbConnectionFactory</c>, una <c>NpgsqlConnection</c>
/// nueva por llamada) no podían cumplir.
///
/// REQUIERE DOCKER: usa <see cref="PostgresTestFixture"/>, que levanta un contenedor
/// postgres:17-alpine real. Si el daemon de Docker no está disponible, la inicialización de la
/// fixture falla y los tests se reportan como fallidos (no como "skip") — nunca como falso verde.
///
/// Dos cosas a propósito de cómo está escrito este archivo, ambas por bugs preexistentes y no
/// relacionados con Task 1.5 (IDbSession), descubiertos al intentar correrlo de verdad contra
/// Docker en este entorno:
///
/// 1) No pasa por <c>WebApplicationFactory&lt;Program&gt;</c> / <c>PostgresTestFixture.CreateFactory()</c>
///    (el camino de <c>OpenSource1.SmokeTests.Api</c>). Ese camino arranca <c>Program.cs</c> completo, y
///    <c>AddApplicationIdentity</c> valida "eager" (antes del <c>Build()</c> del host, antes de que las
///    sobrescrituras de configuración del test se apliquen) que <c>Jwt:SigningKey</c> tenga 32+
///    caracteres. El placeholder de <c>appsettings.json</c> ("__SET_IN_USER_SECRETS__") no llega a
///    32, así que revienta con <c>InvalidOperationException</c> antes de levantar nada. Se reproduce
///    igual, sin tocar nada de esta tarea, en <c>AppSettingsApiTests</c> (ya existente). Por eso aquí
///    se construye directamente el árbol de DI de
///    <c>OpenSource1.Infrastructure.Data.DependencyInjection.AddApplicationData</c> — exactamente la
///    porción que Task 1.5 cambia — sin arrastrar Identity/JWT.
///
/// 2) La escritura de prueba no usa <see cref="ApplicationDbContext"/> con la entidad <c>Entrada</c>
///    real. <c>ApplicationDbContext.OnModelCreating</c> también configura <c>Cliente</c>, cuyo tipo de
///    complejo <c>DireccionCliente</c> tiene un único constructor con un tercer parámetro
///    (<c>nombreCampoLinea1</c>) que no puede enlazarse a ninguna propiedad mapeada. Eso hace que
///    <b>cualquier</b> acceso a <c>ApplicationDbContext.Model</c> (incluido <c>MigrateAsync</c>,
///    incluido <c>SaveChangesAsync</c> sobre <i>cualquier</i> entidad, no solo Cliente) lance
///    <c>InvalidOperationException: No suitable constructor was found for the type
///    'Cliente.Direccion#DireccionCliente'</c> — es un bug de mapeo que ya rompe el uso normal de
///    ApplicationDbContext contra una base real, no algo introducido aquí. Reportado aparte; no se
///    toca en este commit (fuera del alcance de Task 1.5, y <c>DireccionCliente</c>/value objects no
///    están en la lista de archivos de esta tarea). Para no depender de él, la prueba de
///    lectura/escritura usa un <see cref="DbContext"/> mínimo y propio de este test
///    (<see cref="ProbeDbContext"/>), configurado exactamente igual que el registro real de
///    Step 3 (<c>UseNpgsql(session.Connection)</c>), contra su propia tabla. La propiedad que Task 1.5
///    introduce — misma conexión, misma transacción, visible para Dapper — es agnóstica al modelo de
///    entidades: se cumple o no se cumple independientemente de si el modelo es Entrada o esta tabla
///    de prueba. El cableado real de Step 3 (que <see cref="ApplicationDbContext"/> tal cual lo resuelve
///    el contenedor de producción efectivamente usa <see cref="IDbSession.Connection"/>) se verifica
///    aparte, por referencia, en <see cref="ApplicationDbContext_UsaLaMismaConexionQueElDbSession"/>,
///    sin tocar <c>.Model</c>.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DbSessionTests : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private readonly PostgresTestFixture _fixture;
    private ServiceProvider _provider = null!;

    public DbSessionTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
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
        services.AddApplicationData(configuration);
        _provider = services.BuildServiceProvider();

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task ApplicationDbContext_UsaLaMismaConexionQueElDbSession()
    {
        // Verifica el cableado exacto de Step 3 sin tocar ApplicationDbContext.Model (que hoy no
        // se puede construir por el bug de DireccionCliente descrito arriba, ajeno a esta tarea:
        // tanto resolver el DbContext como llamar Database.GetDbConnection() en él fuerzan la
        // construcción del modelo internamente). En su lugar se inspecciona, sin construir el
        // DbContext, las DbContextOptions<ApplicationDbContext> tal como las arma el registro
        // real de Step 3 (options.UseNpgsql(sp.GetRequiredService<DbSession>().Connection)):
        // que la conexión que EF tiene configurada es, por referencia, la misma que expone
        // IDbSession del mismo scope.
        await using var scope = _provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDbSession>();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>();

        var relationalExtension = options.Extensions.OfType<RelationalOptionsExtension>().Single();

        Assert.Same(session.Connection, relationalExtension.Connection);
    }

    [Fact]
    public async Task LecturaDapper_VeEscrituraEfDentroDeLaMismaTransaccion()
    {
        await using var scope = _provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDbSession>();

        await session.EnsureOpenAsync();
        await using var probeContext = CrearProbeDbContext(session);
        await probeContext.Database.EnsureCreatedAsync();

        await using var tx = await session.BeginTransactionAsync();
        await probeContext.Database.UseTransactionAsync(session.CurrentTransaction);

        var item = new ProbeEntity { Nombre = "T" };
        probeContext.Items.Add(item);
        await probeContext.SaveChangesAsync();

        var encontrado = await session.Connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(
                "SELECT \"Id\" FROM \"DbSessionProbe\" WHERE \"Id\" = @Id",
                new { item.Id }, session.CurrentTransaction));

        Assert.Equal(item.Id, encontrado); // con IDbConnectionFactory (conexiones separadas) esto fallaba

        await session.RollbackAsync();
    }

    [Fact]
    public async Task LecturaDapper_NoVeEscrituraEfTrasRollback()
    {
        Guid itemId;

        await using (var scope = _provider.CreateAsyncScope())
        {
            var session = scope.ServiceProvider.GetRequiredService<IDbSession>();

            await session.EnsureOpenAsync();
            await using var probeContext = CrearProbeDbContext(session);
            await probeContext.Database.EnsureCreatedAsync();

            await using (await session.BeginTransactionAsync())
            {
                await probeContext.Database.UseTransactionAsync(session.CurrentTransaction);

                var item = new ProbeEntity { Nombre = "T2" };
                probeContext.Items.Add(item);
                await probeContext.SaveChangesAsync();
                itemId = item.Id;

                await session.RollbackAsync();
            }
        }

        // Scope nuevo, conexión nueva: confirma que el rollback fue real y no un artefacto de
        // reutilizar la misma conexión/transacción todavía viva.
        await using var verificationScope = _provider.CreateAsyncScope();
        var verificationSession = verificationScope.ServiceProvider.GetRequiredService<IDbSession>();
        await verificationSession.EnsureOpenAsync();

        var encontrado = await verificationSession.Connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(
                "SELECT \"Id\" FROM \"DbSessionProbe\" WHERE \"Id\" = @Id",
                new { Id = itemId }));

        Assert.Null(encontrado);
    }

    private static ProbeDbContext CrearProbeDbContext(IDbSession session)
    {
        var options = new DbContextOptionsBuilder<ProbeDbContext>()
            .UseNpgsql(session.Connection)
            .Options;
        return new ProbeDbContext(options);
    }

    private sealed class ProbeEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public required string Nombre { get; set; }
    }

    private sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options) : DbContext(options)
    {
        public DbSet<ProbeEntity> Items => Set<ProbeEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ProbeEntity>(entity =>
            {
                entity.ToTable("DbSessionProbe");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
            });
        }
    }
}
