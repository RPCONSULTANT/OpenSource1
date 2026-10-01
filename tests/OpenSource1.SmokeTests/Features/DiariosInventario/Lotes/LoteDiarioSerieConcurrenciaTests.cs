using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Commands;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Handlers;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Inventario;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Features.DiariosInventario.Lotes;

/// <summary>
/// Residual de feat/no-series (contra Postgres real): el alta y la modificación de un lote de diario leen la serie elegida
/// <c>FOR SHARE</c> dentro de su transacción antes de validar tipo y Activa. Un cambio de tipo (o una desactivación) concurrente
/// que retiene la fila de la serie hace ESPERAR al lote, que después ve la serie ya cambiada y la rechaza; sin el bloqueo, la
/// lectura simple vería la versión anterior confirmada y aceptaría una serie que deja de ser válida. REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class LoteDiarioSerieConcurrenciaTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private readonly LibroInventarioPrueba _prueba = new(fixture);

    public Task InitializeAsync() => _prueba.InicializarAsync();

    public async Task DisposeAsync() => await _prueba.DisposeAsync();

    [Theory]
    [InlineData("""UPDATE "Series" SET "TipoDocumento" = 5 WHERE "Id" = @Id""")]
    [InlineData("""UPDATE "Series" SET "Activa" = false WHERE "Id" = @Id""")]
    public async Task Alta_EsperaAlCambioConcurrenteDeLaSerie_YLaRechaza(string cambio)
    {
        var serieId = await SembrarSerieDeDiarioAsync();
        var codigo = $"L{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        var resultado = await ConCambioRetenidoAsync(serieId, cambio, confirmar: true, async provider =>
        {
            var handler = ActivatorUtilities.CreateInstance<CreateLoteDiarioCommandHandler>(provider);
            return await handler.Handle(new CreateLoteDiarioCommand(PlantillaDiarioIds.Articulo, codigo, "Lote", serieId, false), default);
        });

        Assert.Equal(("diario.serie_invalida", "SerieId"), (resultado.Errores[0].Codigo, resultado.Errores[0].Campo));
        await using var conexion = _prueba.NuevaConexion();
        Assert.Equal(0, await conexion.ExecuteScalarAsync<int>("""SELECT COUNT(*)::int FROM "LotesDiario" WHERE "Codigo" = @C""", new { C = codigo }));
    }

    [Fact]
    public async Task Modificacion_EsperaAlCambioDeTipoConcurrente_YLoRechaza_YSiSeDeshaceLaAcepta()
    {
        var serieId = await SembrarSerieDeDiarioAsync();
        var lote = await SembrarLoteAsync();
        const string cambio = """UPDATE "Series" SET "TipoDocumento" = 5 WHERE "Id" = @Id""";

        Task<Result<LoteDiarioResponse>> Modificar(IServiceProvider provider) =>
            ActivatorUtilities.CreateInstance<UpdateLoteDiarioCommandHandler>(provider)
                .Handle(new UpdateLoteDiarioCommand(lote.Id, lote.Codigo, "Lote", serieId, null, lote.Xmin), default);

        var deshecho = await ConCambioRetenidoAsync(serieId, cambio, confirmar: false, Modificar);
        Assert.True(deshecho.EsExito, deshecho.EsFallo ? deshecho.Errores[0].Codigo : string.Empty);

        await using (var conexion = _prueba.NuevaConexion())
        {
            await conexion.ExecuteAsync("""UPDATE "LotesDiario" SET "SerieId" = NULL WHERE "Id" = @Id""", new { lote.Id });
            lote = lote with { Xmin = await conexion.ExecuteScalarAsync<long>("""SELECT xmin::text::bigint FROM "LotesDiario" WHERE "Id" = @Id""", new { lote.Id }) };
        }

        var rechazado = await ConCambioRetenidoAsync(serieId, cambio, confirmar: true, Modificar);
        Assert.Equal(("diario.serie_invalida", "SerieId"), (rechazado.Errores[0].Codigo, rechazado.Errores[0].Campo));
        await using var lectura = _prueba.NuevaConexion();
        Assert.Null(await lectura.ExecuteScalarAsync<Guid?>("""SELECT "SerieId" FROM "LotesDiario" WHERE "Id" = @Id""", new { lote.Id }));
    }

    /// <summary>
    /// Otra transacción bloquea la serie (<c>FOR UPDATE</c>, como la administración de series) y aplica <paramref name="cambio"/> sin
    /// confirmar; el handler del lote, lanzado mientras tanto, debe quedarse esperando. Después se confirma o deshace el cambio.
    /// </summary>
    private async Task<Result<T>> ConCambioRetenidoAsync<T>(
        Guid serieId, string cambio, bool confirmar, Func<IServiceProvider, Task<Result<T>>> accion)
    {
        await using var admin = new NpgsqlConnection(fixture.AppConnectionString);
        await admin.OpenAsync();
        await using var tx = await admin.BeginTransactionAsync();
        await admin.ExecuteAsync("""SELECT 1 FROM "Series" WHERE "Id" = @Id FOR UPDATE""", new { Id = serieId }, tx);
        await admin.ExecuteAsync(cambio, new { Id = serieId }, tx);

        var tarea = Task.Run(async () =>
        {
            await using var scope = _prueba.Provider.CreateAsyncScope();
            return await accion(scope.ServiceProvider);
        });
        await EsperaBloqueo.EsperarBloqueadaPorAsync(fixture.AppConnectionString, admin.ProcessID, tarea, "El lote debía esperar al bloqueo de la serie");

        if (confirmar)
        {
            await tx.CommitAsync();
        }
        else
        {
            await tx.RollbackAsync();
        }

        return await tarea.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private async Task<Guid> SembrarSerieDeDiarioAsync()
    {
        await using var contexto = _prueba.NuevoContexto();
        var serie = new Serie
        {
            Codigo = $"LDS{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Descripcion = "Serie de diario para concurrencia de lotes",
            TipoDocumento = TipoDocumentoSerie.DiarioInventario,
        };
        contexto.Series.Add(serie);
        await contexto.SaveChangesAsync();
        return serie.Id;
    }

    private async Task<(Guid Id, string Codigo, long Xmin)> SembrarLoteAsync()
    {
        await using var contexto = _prueba.NuevoContexto();
        var lote = new LoteDiario
        {
            PlantillaDiarioId = PlantillaDiarioIds.Articulo,
            Codigo = $"L{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Nombre = "Lote",
            CreatedBy = "test",
        };
        contexto.LotesDiario.Add(lote);
        await contexto.SaveChangesAsync();
        await using var conexion = _prueba.NuevaConexion();
        var xmin = await conexion.ExecuteScalarAsync<long>("""SELECT xmin::text::bigint FROM "LotesDiario" WHERE "Id" = @Id""", new { lote.Id });
        return (lote.Id, lote.Codigo, xmin);
    }
}
