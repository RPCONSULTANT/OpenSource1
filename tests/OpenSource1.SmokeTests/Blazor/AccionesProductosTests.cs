extern alias BlazorApp;

using System.Text.RegularExpressions;
using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Dtos;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Fix-Features C2: acciones contextuales de productos, filtro por estado de existencia y ajuste en diario precargado.</summary>
public sealed class AccionesProductosTests
{
    private static readonly Guid IdProducto = Guid.Parse("7e000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Lista_Tarjeta_YFicha_ConCrearYVer()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var lista = await HtmlSsr.HtmlAsync(app.Cliente(), $"/productos?view=list&sel={IdProducto}");
        var tarjetas = await HtmlSsr.HtmlAsync(app.Cliente(), "/productos?view=grid");
        var ficha = await HtmlSsr.HtmlAsync(app.Cliente(), $"/productos/{IdProducto}");

        foreach (var destino in new[]
                 {
                     $"/diarios-inventario/nuevo?productoId={IdProducto}", $"/inventario/existencias?productoId={IdProducto}",
                     $"/inventario/movimientos-producto?productoId={IdProducto}", $"/inventario/movimientos-valor?productoId={IdProducto}",
                 })
        {
            Assert.Contains($"href=\"{destino}\"", lista);
            Assert.Contains($"href=\"{destino}\"", ficha);
            Assert.Contains($"href=\"{destino}\"", tarjetas);
        }

        Assert.Contains("data-testid=\"menu-crear\"", lista);
        Assert.Contains("data-testid=\"menu-ver\"", lista);
        Assert.Contains("data-testid=\"menu-crear\"", ficha);
        Assert.Contains("data-testid=\"menu-ver\"", ficha);
        Assert.Contains("Ajuste en diario de inventario", ficha);

        // La ficha usa la barra común: Editar vuelve a la ficha, Eliminar abre la confirmación; sin iconos duplicados en la tarjeta.
        Assert.Contains($"<a data-testid=\"accion-editar\" href=\"/productos/{IdProducto}/editar?returnUrl=%2Fproductos%2F{IdProducto}\"", ficha);
        Assert.Contains($"<a data-testid=\"accion-eliminar\" href=\"/productos/{IdProducto}?delete=true\"", ficha);
        Assert.DoesNotContain("title=\"Editar ficha\"", ficha);
        Assert.DoesNotContain("title=\"Eliminar producto\"", ficha);
    }

    [Fact]
    public async Task Lista_SinSeleccion_MenusDeshabilitados()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var lista = await HtmlSsr.HtmlAsync(app.Cliente(), "/productos?view=list");

        Assert.DoesNotContain($"href=\"/diarios-inventario/nuevo?productoId={IdProducto}\"", lista.Split("data-testid=\"menu-crear\"")[1].Split("</details>")[0]);
        Assert.Contains("Ajuste en diario de inventario", lista);
    }

    [Fact]
    public async Task FiltroEstado_EnviaStockStateALaApi_YMuestraElSelector()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var api = app.Simular<IProductoApiClient>();

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), "/productos?filters=estado&estado=without&showFilters=true");

        api.Verify(c => c.ListAsync(It.Is<ProductoSearchFilter?>(f => f!.StockState == "without"), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        Assert.Contains("Estado de existencia", html);
        Assert.Matches("<select name=\"estado\"[^>]*>", html);
        Assert.Matches("<option value=\"without\" selected", html);
        // Buscador superior (oculto) + selector del panel: el panel no añade además su propio oculto (valor duplicado en la query).
        Assert.Equal(2, Regex.Matches(html, "name=\"estado\"").Count);
    }

    [Fact]
    public async Task FiltroEstado_QuitarElCampo_QuitaElEstado()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), "/productos?filters=estado&estado=with&showFilters=true");

        Assert.Contains("href=\"/productos?showFilters=true\" title=\"Quitar campo\"", html);
    }

    [Fact]
    public async Task AjusteEnDiario_TrasCrearElLote_AbreElLoteConElProducto()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var plantilla = Guid.NewGuid();
        var lote = Guid.NewGuid();
        app.Simular<IDiarioInventarioApiClient>()
            .Setup(c => c.CreateLoteAsync(It.IsAny<LoteDiarioInput>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Yield();
                return new LoteDiarioOperationResult(true, "ok", new LoteDiarioResponse { Id = lote });
            });

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), $"/diarios-inventario/nuevo?productoId={IdProducto}");
        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), $"/diarios-inventario/nuevo?productoId={IdProducto}", "save-lote-diario",
            new Dictionary<string, string> { ["SaveInput.PlantillaDiarioId"] = plantilla.ToString(), ["SaveInput.Codigo"] = "AJ-02", ["SaveInput.Nombre"] = "Ajuste" });

        Assert.Contains("Al crear el lote se abrirá con una línea para P0001 — Tornillo.", html);
        Assert.Equal($"/diarios-inventario/{lote}?addProductoId={IdProducto}", FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task NuevoLote_SinProducto_VuelveALaLista()
    {
        using var app = Configurar(new BlazorSsrFactory());
        app.Simular<IDiarioInventarioApiClient>()
            .Setup(c => c.CreateLoteAsync(It.IsAny<LoteDiarioInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoteDiarioOperationResult(true, "ok", new LoteDiarioResponse { Id = Guid.NewGuid() }));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), "/diarios-inventario/nuevo", "save-lote-diario",
            new Dictionary<string, string> { ["SaveInput.PlantillaDiarioId"] = Guid.NewGuid().ToString(), ["SaveInput.Codigo"] = "AJ-03", ["SaveInput.Nombre"] = "Ajuste" });

        Assert.Equal("/diarios-inventario?ok=created", FormulariosSsr.Destino(respuesta));
    }

    private static BlazorSsrFactory Configurar(BlazorSsrFactory app)
    {
        var producto = new ProductoResponse { Id = IdProducto, Codigo = "P0001", Nombre = "Tornillo", CategoriaCodigo = "GENERAL", CategoriaNombre = "General" };
        var productos = app.Simular<IProductoApiClient>();
        productos.Setup(c => c.ListAsync(It.IsAny<ProductoSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Yield();
                return new PagedResult<ProductoResponse>([producto], 1, 50, 1);
            });
        // Respuesta asíncrona real (lección de la puerta B): el primer render ocurre antes de que termine la carga.
        productos.Setup(c => c.GetByIdAsync(IdProducto, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Yield();
                return producto;
            });
        productos.Setup(c => c.GetExistenciasAsync(IdProducto, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<ExistenciaAlmacen>());
        app.Simular<IDiarioInventarioApiClient>().Setup(c => c.ListPlantillasAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        app.Simular<ICategoriaProductoApiClient>();
        app.Simular<IUnidadMedidaApiClient>();
        return app;
    }
}
