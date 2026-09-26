using Microsoft.Extensions.DependencyInjection;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Commands;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Handlers;
using OpenSource1.Application.Features.DiariosInventario.Lotes;
using OpenSource1.Core.Entities.Inventario;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Features.DiariosInventario.Lineas;

/// <summary>
/// Ronda de corrección final de la Fase 4 (punto 3, contra Postgres real): mientras otro usuario tiene el lote
/// bloqueado con <c>FOR UPDATE</c> (misma fila que toma <see cref="ILoteDiarioBloqueoService"/>), editar o borrar una
/// línea del lote ESPERA a que se libere, en vez de colarse antes -- el orden global de locks se mantiene
/// lote -&gt; línea. REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class LineaDiarioLoteBloqueadoConcurrenciaTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private static readonly DateOnly D1 = new(2026, 9, 1);

    private readonly LibroInventarioPrueba _prueba = new(fixture);

    public Task InitializeAsync() => _prueba.InicializarAsync();

    public async Task DisposeAsync() => await _prueba.DisposeAsync();

    [Fact]
    public async Task EditarLineaConLoteBloqueadoPorOtroUsuario_EsperaYLuegoSeAplica()
    {
        var p = await _prueba.SembrarProductoAsync();
        var alm = await _prueba.SembrarAlmacenAsync();
        var lote = await CrearLoteAsync();
        var linea = await AgregarLineaAsync(lote, p, alm, 1m, 1m);

        await using var scopeRetenedor = _prueba.Provider.CreateAsyncScope();
        var sesion = scopeRetenedor.ServiceProvider.GetRequiredService<IDbSession>();
        var loteBloqueo = scopeRetenedor.ServiceProvider.GetRequiredService<ILoteDiarioBloqueoService>();
        await using var tx = await sesion.BeginTransactionAsync();
        Assert.Equal(false, await loteBloqueo.BloquearYObtenerEstadoAsync(lote)); // toma el FOR UPDATE, sin soltarlo

        var tarea = Task.Run(async () =>
        {
            await using var scope = _prueba.Provider.CreateAsyncScope();
            var handler = ActivatorUtilities.CreateInstance<UpdateLineaDiarioCommandHandler>(scope.ServiceProvider);
            return await handler.Handle(new UpdateLineaDiarioCommand(
                linea.Id, D1, D1, null, TipoMovimientoInventario.AjustePositivo, p, alm, null,
                LibroInventarioPrueba.UnidadUnd, 9m, 2m, null, linea.Xmin), default);
        });

        var completoMientrasBloqueado = await Task.WhenAny(tarea, Task.Delay(700)) == tarea;
        await sesion.CommitAsync();

        Assert.False(completoMientrasBloqueado, "La edición de la línea no esperó al lote bloqueado.");
        var resultado = await tarea.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(resultado.EsExito, resultado.EsFallo ? resultado.Errores[0].Codigo : "");
        Assert.Equal(9m, resultado.Valor.Cantidad);
    }

    [Fact]
    public async Task BorrarLineaConLoteBloqueadoPorOtroUsuario_EsperaYLuegoSeAplica()
    {
        var p = await _prueba.SembrarProductoAsync();
        var alm = await _prueba.SembrarAlmacenAsync();
        var lote = await CrearLoteAsync();
        var linea = await AgregarLineaAsync(lote, p, alm, 1m, 1m);

        await using var scopeRetenedor = _prueba.Provider.CreateAsyncScope();
        var sesion = scopeRetenedor.ServiceProvider.GetRequiredService<IDbSession>();
        var loteBloqueo = scopeRetenedor.ServiceProvider.GetRequiredService<ILoteDiarioBloqueoService>();
        await using var tx = await sesion.BeginTransactionAsync();
        Assert.Equal(false, await loteBloqueo.BloquearYObtenerEstadoAsync(lote)); // toma el FOR UPDATE, sin soltarlo

        var tarea = Task.Run(async () =>
        {
            await using var scope = _prueba.Provider.CreateAsyncScope();
            var handler = ActivatorUtilities.CreateInstance<DeleteLineaDiarioCommandHandler>(scope.ServiceProvider);
            return await handler.Handle(new DeleteLineaDiarioCommand(linea.Id), default);
        });

        var completoMientrasBloqueado = await Task.WhenAny(tarea, Task.Delay(700)) == tarea;
        await sesion.CommitAsync();

        Assert.False(completoMientrasBloqueado, "El borrado de la línea no esperó al lote bloqueado.");
        var resultado = await tarea.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(resultado.EsExito, resultado.EsFallo ? resultado.Errores[0].Codigo : "");
    }

    private async Task<Guid> CrearLoteAsync()
    {
        await using var contexto = _prueba.NuevoContexto();
        var lote = new LoteDiario
        {
            PlantillaDiarioId = PlantillaDiarioIds.Articulo,
            Codigo = $"L{Guid.NewGuid():N}"[..15].ToUpperInvariant(),
            Nombre = "Lote de prueba de concurrencia",
            CreatedBy = "test",
        };
        contexto.LotesDiario.Add(lote);
        await contexto.SaveChangesAsync();
        return lote.Id;
    }

    private async Task<LineaDiarioResponse> AgregarLineaAsync(
        Guid loteId, Guid productoId, Guid almacenId, decimal cantidad, decimal costo)
    {
        await using var scope = _prueba.Provider.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<CreateLineaDiarioCommandHandler>(scope.ServiceProvider);
        var resultado = await handler.Handle(new CreateLineaDiarioCommand(
            loteId, D1, D1, null, TipoMovimientoInventario.AjustePositivo, productoId, almacenId, null,
            LibroInventarioPrueba.UnidadUnd, cantidad, costo, null), default);
        Assert.True(resultado.EsExito, resultado.EsFallo ? resultado.Errores[0].Codigo : "");
        return resultado.Valor;
    }
}
