using Dapper;
using Microsoft.Extensions.DependencyInjection;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Commands;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Handlers;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Commands;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Handlers;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Inventario;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Features.DiariosInventario.Lotes;

/// <summary>
/// Ronda de corrección final de la Fase 4 (punto 1, contra Postgres real): carrera entre el alta de una línea y el
/// borrado del lote. Antes del fix, el borrado leía "sin líneas" sin ver la fila que el alta todavía no había
/// confirmado, esperaba en su propio <c>UPDATE</c> de borrado lógico (bloqueado por el <c>FOR UPDATE</c> del lote que
/// el alta sí tomó) y se aplicaba DESPUÉS de que la línea quedara insertada, dejando un lote borrado con una línea
/// viva. Con el fix, ambos toman el mismo <c>FOR UPDATE</c> de la fila del lote y se serializan: nunca queda un lote
/// borrado con líneas vivas (el que pierde la carrera ve 404 si perdió el borrado, o 409 si perdió el alta). REQUIERE
/// DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DeleteLoteDiarioConcurrenciaTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private static readonly DateOnly D1 = new(2026, 9, 1);
    private const int Rondas = 8;

    private readonly LibroInventarioPrueba _prueba = new(fixture);

    public Task InitializeAsync() => _prueba.InicializarAsync();

    public async Task DisposeAsync() => await _prueba.DisposeAsync();

    [Fact]
    public async Task AltaDeLineaYBorradoDelLoteEnParalelo_NuncaQuedaUnLoteBorradoConLineasVivas()
    {
        for (var ronda = 0; ronda < Rondas; ronda++)
        {
            var p = await _prueba.SembrarProductoAsync();
            var alm = await _prueba.SembrarAlmacenAsync();
            var lote = await CrearLoteAsync();

            var barrera = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var listas = 0;

            async Task EsperarBarreraAsync()
            {
                if (Interlocked.Increment(ref listas) == 2)
                {
                    barrera.SetResult();
                }

                await barrera.Task;
            }

            var tareaAlta = Task.Run(async () =>
            {
                await EsperarBarreraAsync();
                await using var scope = _prueba.Provider.CreateAsyncScope();
                var handler = ActivatorUtilities.CreateInstance<CreateLineaDiarioCommandHandler>(scope.ServiceProvider);
                return await handler.Handle(new CreateLineaDiarioCommand(
                    lote, D1, D1, null, TipoMovimientoInventario.AjustePositivo, p, alm, null,
                    LibroInventarioPrueba.UnidadUnd, 1m, 1m, null), default);
            });

            var tareaBorrado = Task.Run(async () =>
            {
                await EsperarBarreraAsync();
                await using var scope = _prueba.Provider.CreateAsyncScope();
                var handler = ActivatorUtilities.CreateInstance<DeleteLoteDiarioCommandHandler>(scope.ServiceProvider);
                return await handler.Handle(new DeleteLoteDiarioCommand(lote), default);
            });

            await Task.WhenAll(tareaAlta, tareaBorrado).WaitAsync(TimeSpan.FromSeconds(30));
            var altaResultado = await tareaAlta;
            var borradoResultado = await tareaBorrado;

            var (loteBorrado, lineasVivas) = await EstadoDelLoteAsync(lote);

            // Invariante central: nunca un lote borrado con una línea viva.
            Assert.False(loteBorrado && lineasVivas > 0,
                $"Ronda {ronda}: lote borrado={loteBorrado}, líneas vivas={lineasVivas} " +
                $"(alta: {Describir(altaResultado)}; borrado: {Describir(borradoResultado)}).");

            if (borradoResultado.EsExito)
            {
                // El borrado ganó la carrera: el lote quedó borrado y sin líneas, y el alta debió ver el lote
                // borrado (404) o llegar antes de que el borrado empezara a comprobar líneas.
                Assert.True(loteBorrado);
                Assert.Equal(0, lineasVivas);
                if (altaResultado.EsFallo)
                {
                    Assert.Equal("diario_lote.no_encontrado", altaResultado.Errores[0].Codigo);
                }
            }
            else
            {
                // El alta ganó (o llegó primero): el lote sigue vivo con su línea, y el borrado debió ver la
                // línea recién confirmada (409 diario.conflicto) o el lote bloqueado nunca aplica aquí.
                Assert.True(altaResultado.EsExito, altaResultado.EsFallo ? altaResultado.Errores[0].Codigo : "");
                Assert.False(loteBorrado);
                Assert.Equal(1, lineasVivas);
                Assert.Equal("diario.conflicto", borradoResultado.Errores[0].Codigo);
            }
        }
    }

    private static string Describir(Result resultado) =>
        resultado.EsExito ? "éxito" : string.Join("; ", resultado.Errores.Select(e => e.Codigo));

    private static string Describir<T>(Result<T> resultado) =>
        resultado.EsExito ? "éxito" : string.Join("; ", resultado.Errores.Select(e => e.Codigo));

    private async Task<(bool Borrado, int LineasVivas)> EstadoDelLoteAsync(Guid loteId)
    {
        await using var conexion = _prueba.NuevaConexion();
        var borrado = await conexion.ExecuteScalarAsync<bool>(
            """SELECT "IsDeleted" FROM "LotesDiario" WHERE "Id" = @Id""", new { Id = loteId });
        var lineasVivas = await conexion.ExecuteScalarAsync<int>(
            """SELECT COUNT(*)::int FROM "LineasDiario" WHERE "LoteDiarioId" = @Id AND "IsDeleted" = false""", new { Id = loteId });
        return (borrado, lineasVivas);
    }

    private async Task<Guid> CrearLoteAsync()
    {
        await using var contexto = _prueba.NuevoContexto();
        var lote = new LoteDiario
        {
            PlantillaDiarioId = PlantillaDiarioIds.Articulo,
            Codigo = $"L{Guid.NewGuid():N}"[..15].ToUpperInvariant(),
            Nombre = "Lote de prueba de concurrencia borrado/alta",
            CreatedBy = "test",
        };
        contexto.LotesDiario.Add(lote);
        await contexto.SaveChangesAsync();
        return lote.Id;
    }
}
