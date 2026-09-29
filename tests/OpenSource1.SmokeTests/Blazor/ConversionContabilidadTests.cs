extern alias BlazorApp;

using System.Net;
using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.CuentasContables.Dtos;
using OpenSource1.Application.Features.GruposContables.Dtos;
using OpenSource1.Application.Features.SetupsContables.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;
using static OpenSource1.SmokeTests.TestInfrastructure.HtmlSsr;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Fix-Features B2b: maestros contables con alta y edición en página-tarjeta; el tipo (grupos/setups) viaja en la query.</summary>
public sealed class ConversionContabilidadTests
{
    private static readonly Guid IdCuenta = Guid.Parse("7b000000-0000-0000-0000-000000000001");

    [Theory]
    [InlineData("/cuentas-contables/nuevo", "save-cuenta-contable", "/cuentas-contables")]
    [InlineData("/grupos-contables/nuevo?tipo=producto", "save-grupo-contable", "/grupos-contables?tipo=producto")]
    [InlineData("/grupos-cliente-contable/nuevo", "save-grupo-cliente-contable", "/grupos-cliente-contable")]
    [InlineData("/setups-contables/nuevo?tipo=iva", "save-setup-contable", "/setups-contables?tipo=iva")]
    public async Task Nuevo_RenderizaFormularioEnTarjeta(string ruta, string formName, string lista)
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente(), ruta);

        Assert.Contains("data-testid=\"entity-form-page\"", html);
        Assert.Contains($"name=\"_handler\" value=\"{formName}\"", html);
        Assert.Contains($"href=\"{lista}\" data-testid=\"cancelar\"", html);
        Assert.DoesNotContain("data-testid=\"page-toolbar\"", html);
    }

    // R15: la ruta antigua ?editId= redirige a ConRetorno("/x/{id}/editar", UrlActual): se comprueba la ruta (con su tipo)
    // y que el returnUrl empiece por la lista, no la URL exacta.
    [Theory]
    [InlineData("/cuentas-contables?editId={0}", "/cuentas-contables/{0}/editar?", "/cuentas-contables")]
    [InlineData("/grupos-contables?tipo=producto&editId={0}", "/grupos-contables/{0}/editar?tipo=producto&", "/grupos-contables?tipo=producto")]
    [InlineData("/grupos-cliente-contable?editId={0}", "/grupos-cliente-contable/{0}/editar?", "/grupos-cliente-contable")]
    [InlineData("/setups-contables?tipo=iva&editId={0}", "/setups-contables/{0}/editar?tipo=iva&", "/setups-contables?tipo=iva")]
    public async Task EditIdLegado_RedirigeALaRutaNueva(string origen, string destino, string lista)
    {
        using var app = Configurar(new BlazorSsrFactory());
        var id = Guid.NewGuid();

        var respuesta = await app.Cliente().GetAsync(string.Format(origen, id));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        var location = FormulariosSsr.Destino(respuesta);
        Assert.StartsWith(string.Format(destino, id) + "returnUrl=", location);
        var returnUrl = Uri.UnescapeDataString(location[(location.IndexOf("returnUrl=", StringComparison.Ordinal) + "returnUrl=".Length)..]);
        Assert.StartsWith(lista, returnUrl);
        Assert.DoesNotContain("editId", returnUrl);
    }

    [Theory]
    [InlineData("/cuentas-contables", "save-cuenta-contable", "/cuentas-contables/nuevo?returnUrl=")]
    [InlineData("/grupos-contables?tipo=producto", "save-grupo-contable", "/grupos-contables/nuevo?tipo=producto&returnUrl=")]
    [InlineData("/grupos-cliente-contable", "save-grupo-cliente-contable", "/grupos-cliente-contable/nuevo?returnUrl=")]
    [InlineData("/setups-contables?tipo=iva", "save-setup-contable", "/setups-contables/nuevo?tipo=iva&returnUrl=")]
    public async Task Listado_SinFormulariosEnLinea_ConBarra(string ruta, string formNameAlta, string nuevoHref)
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente(), ruta);

        Assert.Contains("data-testid=\"page-toolbar\"", html);
        Assert.Contains("data-testid=\"accion-nuevo\"", html);
        Assert.Contains($"href=\"{nuevoHref}", html);
        Assert.DoesNotContain("id=\"agregar\"", html);
        Assert.DoesNotContain("id=\"modificar\"", html);
        Assert.DoesNotContain($"value=\"{formNameAlta}\"", html);
    }

    [Fact]
    public async Task CuentaContable_Seleccion_ValidaHabilitaEditar_YLaQueNoEstaEnLaPaginaLaDeshabilita()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var valida = await HtmlAsync(app.Cliente(), $"/cuentas-contables?sel={IdCuenta}");
        var invalida = await HtmlAsync(app.Cliente(), $"/cuentas-contables?sel={Guid.NewGuid()}");

        Assert.Contains($"<a data-testid=\"accion-editar\" href=\"/cuentas-contables/{IdCuenta}/editar?returnUrl=", valida);
        Assert.Contains("aria-current=\"true\"", valida);
        Assert.Contains("data-testid=\"seleccionar-fila\"", valida);
        Assert.Contains("<span data-testid=\"accion-editar\" aria-disabled=\"true\"", invalida);
    }

    [Fact]
    public async Task CuentaContable_Editar_CargaLoGuardado()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente(), $"/cuentas-contables/{IdCuenta}/editar");

        Assert.Contains("name=\"_handler\" value=\"update-cuenta-contable\"", html);
        Assert.Contains("value=\"1101\"", html);
        Assert.Contains("name=\"UpdateInput.Xmin\" value=\"41\"", html);
        Assert.Contains("data-testid=\"guardar\"", html);
    }

    [Fact]
    public async Task CuentaContable_Editar_NoEncontrado_Mensaje()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente(), $"/cuentas-contables/{Guid.NewGuid()}/editar");

        Assert.Contains("No se encontró la cuenta contable seleccionada", html);
        Assert.DoesNotContain("data-testid=\"guardar\"", html);
    }

    [Fact]
    public async Task CuentaContable_Editar_EnviaElXminLeido()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var api = app.Simular<ICuentaContableApiClient>();
        api.Setup(c => c.UpdateAsync(IdCuenta, It.IsAny<CuentaContableInput>(), 41, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CuentaContableOperationResult(true, "ok"));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), $"/cuentas-contables/{IdCuenta}/editar", "update-cuenta-contable",
            new Dictionary<string, string>
            {
                ["UpdateInput.Id"] = IdCuenta.ToString(), ["UpdateInput.Xmin"] = "41",
                ["UpdateInput.Numero"] = "1101", ["UpdateInput.Nombre"] = "Caja general",
            });

        Assert.Equal("/cuentas-contables?ok=updated", FormulariosSsr.Destino(respuesta));
        api.Verify(c => c.UpdateAsync(IdCuenta, It.Is<CuentaContableInput>(i => i.Nombre == "Caja general"), 41, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CuentaContable_Nuevo_SinCanAdd_Mensaje_YPostForzado_DevuelveMensajeDeLaApi()
    {
        using var app = Configurar(new BlazorSsrFactory());
        app.Simular<ICuentaContableApiClient>()
            .Setup(c => c.CreateAsync(It.IsAny<CuentaContableInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CuentaContableOperationResult(false, "No tiene permisos para agregar cuentas contables."));
        var supervisor = app.Cliente("Supervisor");

        var html = await HtmlAsync(supervisor, "/cuentas-contables/nuevo");
        var forzado = await FormulariosSsr.EnviarAsync(supervisor, "/cuentas-contables/nuevo", "save-cuenta-contable",
            new Dictionary<string, string> { ["SaveInput.Numero"] = "1101", ["SaveInput.Nombre"] = "Caja" });

        Assert.Contains("No tiene permiso para realizar esta acción.", html);
        Assert.Contains("value=\"save-cuenta-contable\"", html);
        Assert.DoesNotContain("data-testid=\"guardar\"", html);
        Assert.Equal(HttpStatusCode.OK, forzado.StatusCode);
        Assert.Contains("No tiene permisos para agregar cuentas contables.", Decodificar(await forzado.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task GrupoContable_GuardarVuelveAlTipo()
    {
        using var app = Configurar(new BlazorSsrFactory());
        app.Simular<IGrupoContableApiClient>()
            .Setup(c => c.CreateAsync(TipoGrupoContable.Producto, It.IsAny<GrupoContableInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GrupoOperationResult(true, "ok"));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), "/grupos-contables/nuevo?tipo=producto&returnUrl=%2Fgrupos-contables%3Ftipo%3Dproducto",
            "save-grupo-contable", new Dictionary<string, string> { ["SaveInput.Codigo"] = "SERV", ["SaveInput.Descripcion"] = "Servicios" });

        Assert.Equal("/grupos-contables?tipo=producto&ok=created", FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task SetupContable_ReturnUrlExterno_CancelarVuelveAlTipo()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente(), "/setups-contables/nuevo?tipo=inventario&returnUrl=%2F%2Fevil.com");

        Assert.Contains("href=\"/setups-contables?tipo=inventario\" data-testid=\"cancelar\"", html);
    }

    [Theory]
    [InlineData("/grupos-contables/nuevo?tipo=xyz", "El tipo de grupo 'xyz' no existe")]
    [InlineData("/setups-contables/nuevo?tipo=xyz", "El tipo de setup 'xyz' no existe")]
    public async Task Tarjeta_TipoInvalido_Avisa(string ruta, string aviso)
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlAsync(app.Cliente(), ruta);

        Assert.Contains("data-testid=\"entity-form-page\"", html);
        Assert.Contains(aviso, html);
    }

    [Fact]
    public async Task SetupContable_Tarjeta_SinPermiso_NoCargaOpciones()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var grupos = app.Simular<IGrupoContableApiClient>();
        var cuentas = app.Simular<ICuentaContableApiClient>();

        var html = await HtmlAsync(app.Cliente("Supervisor"), "/setups-contables/nuevo?tipo=general");

        Assert.Contains("No tiene permiso para realizar esta acción.", html);
        Assert.Contains("value=\"save-setup-contable\"", html);
        grupos.Verify(c => c.ListAllAsync(It.IsAny<TipoGrupoContable>(), It.IsAny<CancellationToken>()), Times.Never);
        cuentas.Verify(c => c.ListAsync(It.IsAny<CuentaContableSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GrupoClienteContable_Editar_NoEncontrado_NoCargaCuentas()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var cuentas = app.Simular<ICuentaContableApiClient>();

        var html = await HtmlAsync(app.Cliente(), $"/grupos-cliente-contable/{Guid.NewGuid()}/editar");

        Assert.Contains("No se encontró el grupo", html);
        cuentas.Verify(c => c.ListAsync(It.IsAny<CuentaContableSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetupContable_EditarIva_CargaLoGuardado_YGuardaConXmin()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var idSetup = Guid.Parse("7b000000-0000-0000-0000-0000000000c1");
        var idIvaNegocio = Guid.Parse("7b000000-0000-0000-0000-0000000000c2");
        var idIvaProducto = Guid.Parse("7b000000-0000-0000-0000-0000000000c3");
        var grupos = app.Simular<IGrupoContableApiClient>();
        grupos.Setup(c => c.ListAllAsync(TipoGrupoContable.IvaNegocio, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GrupoContableResponse { Id = idIvaNegocio, Codigo = "NAC", Descripcion = "Nacional" }]);
        grupos.Setup(c => c.ListAllAsync(TipoGrupoContable.IvaProducto, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GrupoContableResponse { Id = idIvaProducto, Codigo = "ITBIS18", Descripcion = "ITBIS 18" }]);
        var setups = app.Simular<ISetupContableApiClient>();
        setups.Setup(c => c.GetByIdAsync<SetupIvaResponse>(TipoSetupContable.Iva, idSetup, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SetupIvaResponse
            {
                Id = idSetup, Xmin = 9, GrupoIvaNegocioId = idIvaNegocio, GrupoIvaNegocioCodigo = "NAC", GrupoIvaProductoId = idIvaProducto,
                GrupoIvaProductoCodigo = "ITBIS18", PorcentajeIva = 18m, CuentaIvaVentasId = IdCuenta, CuentaIvaVentasNumero = "1101",
                CuentaIvaVentasNombre = "Caja", IdentificadorIva = "ITBIS18",
            });
        setups.Setup(c => c.UpdateAsync(TipoSetupContable.Iva, idSetup, It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GrupoOperationResult(true, "ok"));
        var url = $"/setups-contables/{idSetup}/editar?tipo=iva&returnUrl=%2Fsetups-contables%3Ftipo%3Diva";

        var html = await HtmlAsync(app.Cliente(), url);
        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), url, "update-setup-contable", new Dictionary<string, string>
        {
            ["UpdateInput.Id"] = idSetup.ToString(), ["UpdateInput.Xmin"] = "9", ["UpdateInput.SecundarioId"] = idIvaNegocio.ToString(),
            ["UpdateInput.PrincipalId"] = idIvaProducto.ToString(), ["UpdateInput.Cuenta1Id"] = IdCuenta.ToString(),
            ["UpdateInput.PorcentajeIvaTexto"] = "16.00", ["UpdateInput.IdentificadorIva"] = "ITBIS16", ["UpdateInput.TipoCalculoIva"] = "1",
        });

        Assert.Contains("data-testid=\"entity-form-page\"", html);
        Assert.Contains("name=\"_handler\" value=\"update-setup-contable\"", html);
        Assert.Contains("name=\"UpdateInput.Xmin\" value=\"9\"", html);
        Assert.Contains("value=\"ITBIS18\"", html);
        Assert.Contains("href=\"/setups-contables?tipo=iva\" data-testid=\"cancelar\"", html);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/setups-contables?tipo=iva&ok=updated", FormulariosSsr.Destino(respuesta));
        setups.Verify(c => c.UpdateAsync(TipoSetupContable.Iva, idSetup,
            It.Is<object>(o => (long)o.GetType().GetProperty("Xmin")!.GetValue(o)! == 9
                && (string)o.GetType().GetProperty("IdentificadorIva")!.GetValue(o)! == "ITBIS16"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static BlazorSsrFactory Configurar(BlazorSsrFactory app)
    {
        var cuenta = new CuentaContableResponse { Id = IdCuenta, Numero = "1101", Nombre = "Caja", Xmin = 41 };
        app.Simular<ICuentaContableApiClient>().Setup(c => c.ListAsync(It.IsAny<CuentaContableSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<CuentaContableResponse>([cuenta], 1, 50, 1));
        app.Simular<ICuentaContableApiClient>().Setup(c => c.GetByIdAsync(IdCuenta, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cuenta);
        app.Simular<IGrupoContableApiClient>();
        app.Simular<IGrupoClienteContableApiClient>();
        app.Simular<ISetupContableApiClient>();
        app.Simular<IAlmacenApiClient>();
        return app;
    }
}
