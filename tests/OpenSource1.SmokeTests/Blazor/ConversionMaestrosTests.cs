extern alias BlazorApp;

using System.Net;
using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.Almacenes.Dtos;
using OpenSource1.Application.Features.CategoriasProducto.Dtos;
using OpenSource1.Application.Features.TerminosPago.Dtos;
using OpenSource1.Application.Features.UnidadesMedida.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.SmokeTests.TestInfrastructure;
using static OpenSource1.SmokeTests.TestInfrastructure.HtmlSsr;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Fix-Features B2a: alta y edición de maestros en página-tarjeta propia; listados con barra y selección.</summary>
public sealed class ConversionMaestrosTests
{
    private static readonly Guid IdUnidad = Guid.Parse("7a000000-0000-0000-0000-000000000001");

    [Theory]
    [InlineData("/unidades-medida/nuevo", "save-unidadmedida", "/unidades-medida")]
    [InlineData("/terminos-pago/nuevo", "save-terminopago", "/terminos-pago")]
    [InlineData("/categorias-producto/nuevo", "save-categoriaproducto", "/categorias-producto")]
    [InlineData("/almacenes/nuevo", "save-almacen", "/almacenes")]
    public async Task Nuevo_Admin_RenderizaFormularioEnTarjeta(string ruta, string formName, string lista)
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente(), ruta);

        Assert.Contains("data-testid=\"entity-form-page\"", html);
        Assert.Contains($"name=\"_handler\" value=\"{formName}\"", html);
        Assert.Contains("data-testid=\"guardar\"", html);
        Assert.Contains($"href=\"{lista}\" data-testid=\"cancelar\"", html);
    }

    [Theory]
    [InlineData("/unidades-medida?codigo=K&editId={0}", "/unidades-medida/{0}/editar", "/unidades-medida?codigo=K")]
    [InlineData("/terminos-pago?editId={0}", "/terminos-pago/{0}/editar", "/terminos-pago")]
    [InlineData("/categorias-producto?editId={0}", "/categorias-producto/{0}/editar", "/categorias-producto")]
    [InlineData("/almacenes?editId={0}", "/almacenes/{0}/editar", "/almacenes")]
    public async Task EditIdLegado_RedirigeALaRutaNueva(string origen, string destino, string lista)
    {
        using var app = Configurar(new BlazorSsrFactory());
        var id = Guid.NewGuid();

        var respuesta = await app.Cliente().GetAsync(string.Format(origen, id));

        // R15: la ruta nueva lleva returnUrl a la lista (con sus filtros y sin editId, para no volver a redirigir).
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal($"{string.Format(destino, id)}?returnUrl={Uri.EscapeDataString(lista)}", FormulariosSsr.Destino(respuesta));
    }

    [Theory]
    [InlineData("/unidades-medida", "save-unidadmedida")]
    [InlineData("/terminos-pago", "save-terminopago")]
    [InlineData("/categorias-producto", "save-categoriaproducto")]
    [InlineData("/almacenes", "save-almacen")]
    public async Task Listado_SinFormulariosEnLinea_ConBarra(string ruta, string formNameAlta)
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente(), ruta);

        Assert.Contains("data-testid=\"page-toolbar\"", html);
        Assert.Contains($"href=\"{ruta}/nuevo?returnUrl=", html);
        Assert.DoesNotContain("id=\"agregar\"", html);
        Assert.DoesNotContain("id=\"modificar\"", html);
        Assert.DoesNotContain($"value=\"{formNameAlta}\"", html);
    }

    [Fact]
    public async Task Seleccion_ValidaHabilitaEditar_YLaQueNoEstaEnLaPaginaLaDeshabilita()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var valida = await HtmlAsync(app.Cliente(), $"/unidades-medida?sel={IdUnidad}");
        var invalida = await HtmlAsync(app.Cliente(), $"/unidades-medida?sel={Guid.NewGuid()}");

        Assert.Contains($"<a data-testid=\"accion-editar\" href=\"/unidades-medida/{IdUnidad}/editar?returnUrl=", valida);
        Assert.Contains("aria-current=\"true\"", valida);
        Assert.Contains("<span data-testid=\"accion-editar\" aria-disabled=\"true\"", invalida);
        Assert.DoesNotContain("aria-current=\"true\"", invalida);
    }

    [Fact]
    public async Task Eliminar_DesdeLaBarra_CancelarConservaFiltrosYSeleccion()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente(), $"/unidades-medida?codigo=U&sel={IdUnidad}&deleteId={IdUnidad}");

        Assert.Contains("value=\"delete-unidadmedida\"", html);
        Assert.Contains($"href=\"/unidades-medida?codigo=U&sel={IdUnidad}\"", html);
    }

    [Fact]
    public async Task Eliminar_Confirmado_VuelveALaListaConFiltrosYOk()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var api = app.Simular<IUnidadMedidaApiClient>();
        api.Setup(c => c.DeleteAsync(IdUnidad, It.IsAny<CancellationToken>())).ReturnsAsync(new UnidadMedidaOperationResult(true, "ok"));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), $"/unidades-medida?codigo=U&deleteId={IdUnidad}", "delete-unidadmedida",
            new Dictionary<string, string> { ["DeleteInput.Id"] = IdUnidad.ToString() });

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/unidades-medida?codigo=U&ok=deleted", FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task Guardar_Alta_RedirigeAlReturnUrlConOk()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var api = app.Simular<IUnidadMedidaApiClient>();
        api.Setup(c => c.CreateAsync(It.Is<UnidadMedidaInput>(i => i.Codigo == "KG" && i.Decimales == 2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UnidadMedidaOperationResult(true, "ok", Guid.NewGuid()));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), "/unidades-medida/nuevo?returnUrl=%2Funidades-medida%3Fcodigo%3DK",
            "save-unidadmedida", new Dictionary<string, string> { ["SaveInput.Codigo"] = " KG ", ["SaveInput.Nombre"] = "Kilogramo", ["SaveInput.Decimales"] = "2" });

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/unidades-medida?codigo=K&ok=created", FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task Guardar_ConReturnUrlExterno_VuelveALaLista()
    {
        using var app = Configurar(new BlazorSsrFactory());
        app.Simular<IUnidadMedidaApiClient>()
            .Setup(c => c.CreateAsync(It.IsAny<UnidadMedidaInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UnidadMedidaOperationResult(true, "ok"));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), "/unidades-medida/nuevo?returnUrl=%2F%2Fevil.com",
            "save-unidadmedida", new Dictionary<string, string> { ["SaveInput.Codigo"] = "KG", ["SaveInput.Nombre"] = "Kilogramo", ["SaveInput.Decimales"] = "2" });

        Assert.Equal("/unidades-medida?ok=created", FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task Editar_CargaLoGuardado_YGuardaConElIdDeLaRuta()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var api = app.Simular<IUnidadMedidaApiClient>();
        api.Setup(c => c.UpdateAsync(IdUnidad, It.Is<UnidadMedidaInput>(i => i.Codigo == "UNI"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UnidadMedidaOperationResult(true, "ok"));

        var html = await HtmlAsync(app.Cliente(), $"/unidades-medida/{IdUnidad}/editar");
        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), $"/unidades-medida/{IdUnidad}/editar", "update-unidadmedida",
            new Dictionary<string, string> { ["UpdateInput.Id"] = IdUnidad.ToString(), ["UpdateInput.Codigo"] = "UNI", ["UpdateInput.Nombre"] = "Unidad", ["UpdateInput.Decimales"] = "0" });

        Assert.Contains("value=\"UND\"", html);
        Assert.Equal("/unidades-medida?ok=updated", FormulariosSsr.Destino(respuesta));
        api.Verify(c => c.UpdateAsync(IdUnidad, It.IsAny<UnidadMedidaInput>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Editar_NoEncontrado_Mensaje()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente(), $"/unidades-medida/{Guid.NewGuid()}/editar");

        Assert.Contains("No se encontró la unidad de medida seleccionada", html);
        Assert.DoesNotContain("data-testid=\"guardar\"", html);
    }

    [Fact]
    public async Task Nuevo_SinCanAdd_Mensaje_YPostForzado_DevuelveMensajeDeLaApi()
    {
        using var app = Configurar(new BlazorSsrFactory());
        app.Simular<IUnidadMedidaApiClient>()
            .Setup(c => c.CreateAsync(It.IsAny<UnidadMedidaInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UnidadMedidaOperationResult(false, "No tiene permisos para agregar unidades de medida."));
        var supervisor = app.Cliente("Supervisor");

        var html = await HtmlAsync(supervisor, "/unidades-medida/nuevo");
        var forzado = await FormulariosSsr.EnviarAsync(supervisor, "/unidades-medida/nuevo", "save-unidadmedida",
            new Dictionary<string, string> { ["SaveInput.Codigo"] = "KG", ["SaveInput.Nombre"] = "Kilogramo", ["SaveInput.Decimales"] = "2" });

        Assert.Contains("No tiene permiso para realizar esta acción.", html);
        Assert.Contains("value=\"save-unidadmedida\"", html);
        Assert.DoesNotContain("data-testid=\"guardar\"", html);
        Assert.Equal(HttpStatusCode.OK, forzado.StatusCode);
        Assert.Contains("No tiene permisos para agregar unidades de medida.", Decodificar(await forzado.Content.ReadAsStringAsync()));
    }

    private static BlazorSsrFactory Configurar(BlazorSsrFactory app)
    {
        app.Simular<IUnidadMedidaApiClient>().Setup(c => c.ListAsync(It.IsAny<UnidadMedidaSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<UnidadMedidaResponse>([new UnidadMedidaResponse { Id = IdUnidad, Codigo = "UND", Nombre = "Unidad" }], 1, 50, 1));
        app.Simular<IUnidadMedidaApiClient>().Setup(c => c.GetByIdAsync(IdUnidad, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UnidadMedidaResponse { Id = IdUnidad, Codigo = "UND", Nombre = "Unidad" });
        app.Simular<ITerminoPagoApiClient>().Setup(c => c.ListAsync(It.IsAny<TerminoPagoSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<TerminoPagoResponse>([], 1, 50, 0));
        app.Simular<ICategoriaProductoApiClient>().Setup(c => c.ListAsync(It.IsAny<CategoriaProductoSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<CategoriaProductoResponse>([], 1, 50, 0));
        app.Simular<ICategoriaProductoApiClient>().Setup(c => c.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CategoriaProductoResponse { Id = Guid.NewGuid(), Codigo = "GENERAL", Nombre = "General" }]);
        app.Simular<IAlmacenApiClient>().Setup(c => c.ListAsync(It.IsAny<AlmacenSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<AlmacenResponse>([], 1, 50, 0));
        return app;
    }
}
