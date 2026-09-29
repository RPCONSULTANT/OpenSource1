extern alias BlazorApp;

using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.Cobros;
using OpenSource1.Application.Features.CuentasContables.Dtos;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Fix-Features C1: acciones contextuales de clientes (lista, tarjeta y ficha) y alta de cobro en página propia.</summary>
public sealed class AccionesClientesTests
{
    private static readonly Guid IdCliente = Guid.Parse("7d000000-0000-0000-0000-000000000001");
    private static readonly Guid IdCaja = Guid.Parse("7d000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task Lista_ConSeleccion_CrearYVerApuntanAlCliente()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), $"/clientes?view=list&sel={IdCliente}");
        // Puerta C: las acciones Crear llevan la lista (filtros y selección) como returnUrl; las Ver no.
        var retorno = Uri.EscapeDataString($"/clientes?view=list&sel={IdCliente}");

        foreach (var destino in new[]
                 {
                     $"/facturas-venta/nueva?socioId={IdCliente}&returnUrl={retorno}", $"/notas-credito-venta/nueva?socioId={IdCliente}&returnUrl={retorno}",
                     $"/cobros/nuevo?socioId={IdCliente}&returnUrl={retorno}",
                     $"/clientes/{IdCliente}", $"/ventas/movimientos-cliente?socioId={IdCliente}", $"/ventas/estado-cuenta?socioId={IdCliente}",
                     $"/cobros?socioId={IdCliente}", $"/facturas-venta?socioId={IdCliente}",
                 })
        {
            Assert.Contains($"href=\"{destino}\"", html);
        }

        Assert.Contains("data-testid=\"menu-crear\"", html);
        Assert.Contains("data-testid=\"menu-ver\"", html);
    }

    [Fact]
    public async Task Lista_SinSeleccion_AccionesDelMenuDeshabilitadas()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), "/clientes?view=list");

        Assert.DoesNotContain($"href=\"/cobros/nuevo?socioId={IdCliente}", html);
        // El aviso se comprueba DENTRO del menú Crear (Editar/Eliminar de la barra también lo llevan).
        var menu = System.Text.RegularExpressions.Regex.Match(html, "<details[^>]*data-testid=\"menu-crear\".*?</details>", System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(menu.Success);
        Assert.Contains($"<span aria-disabled=\"true\" tabindex=\"0\" title=\"{(BlazorApp::OpenSource1.Blazor.Components.PageToolbar.SinSeleccion)}\"", menu.Value);
        Assert.DoesNotContain("<a href=", menu.Value);
    }

    [Fact]
    public async Task Supervisor_NoVeFacturaNiNota_PeroSiCobro()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente("Supervisor"), $"/clientes?view=list&sel={IdCliente}");

        Assert.DoesNotContain($"href=\"/facturas-venta/nueva?socioId={IdCliente}", html);
        Assert.DoesNotContain($"href=\"/notas-credito-venta/nueva?socioId={IdCliente}", html);
        Assert.Contains($"href=\"/cobros/nuevo?socioId={IdCliente}&returnUrl=", html);
    }

    [Fact]
    public async Task Ejecutor_NoVeCobro_PeroSiFactura()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente("Ejecutor"), $"/clientes?view=list&sel={IdCliente}");

        Assert.Contains($"href=\"/facturas-venta/nueva?socioId={IdCliente}&returnUrl=", html);
        Assert.DoesNotContain($"href=\"/cobros/nuevo?socioId={IdCliente}", html);
    }

    [Fact]
    public async Task Tarjeta_YFicha_TienenLasMismasAcciones()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var tarjetas = await HtmlSsr.HtmlAsync(app.Cliente(), "/clientes?view=grid");
        var ficha = await HtmlSsr.HtmlAsync(app.Cliente(), $"/clientes/{IdCliente}");

        Assert.Contains($"href=\"/facturas-venta/nueva?socioId={IdCliente}&returnUrl=", tarjetas);
        Assert.Contains($"href=\"/cobros/nuevo?socioId={IdCliente}&returnUrl=", tarjetas);
        Assert.Contains($"href=\"/ventas/estado-cuenta?socioId={IdCliente}\"", tarjetas);
        Assert.Contains("data-testid=\"page-toolbar\"", ficha);
        Assert.Contains($"href=\"/facturas-venta/nueva?socioId={IdCliente}&returnUrl=", ficha);
        Assert.Contains($"href=\"/ventas/estado-cuenta?socioId={IdCliente}\"", ficha);
        Assert.Contains($"<a data-testid=\"accion-editar\" href=\"/clientes/{IdCliente}/editar?returnUrl=", ficha);
        Assert.Contains($"<a data-testid=\"accion-eliminar\" href=\"/clientes/{IdCliente}?delete=true\"", ficha);
        Assert.Contains("Comercial Uno", ficha);
    }

    [Fact]
    public async Task CobroNuevo_PrecargaCliente_YAlGuardarVuelveACobros()
    {
        using var app = Configurar(new BlazorSsrFactory(), cargaLenta: true);
        app.Simular<ICobroApiClient>()
            .Setup(c => c.RegistrarPagoAsync(It.Is<RegistrarPagoInput>(i => i.SocioNegocioId == IdCliente && i.Importe == 500m), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<ResultadoPagoCliente>(true, "ok", new ResultadoPagoCliente("PAG-0001", 9, "CONTAB-1")));

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), $"/cobros/nuevo?socioId={IdCliente}");
        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), $"/cobros/nuevo?socioId={IdCliente}", "registrar-pago",
            new Dictionary<string, string>
            {
                ["PagoInput.SocioNegocioId"] = IdCliente.ToString(), ["PagoInput.ImporteTexto"] = "500.00",
                ["PagoInput.FechaRegistroTexto"] = "2026-09-28", ["PagoInput.CuentaCajaId"] = IdCaja.ToString(),
            });

        Assert.Contains("data-testid=\"entity-form-page\"", html);
        Assert.Contains($"<option value=\"{IdCliente}\" selected", html);
        Assert.Contains($"<option value=\"{IdCaja}\" selected", html);
        Assert.Contains($"href=\"/cobros?socioId={IdCliente}\" data-testid=\"cancelar\"", html);
        Assert.Equal($"/cobros?socioId={IdCliente}&ok=pago&numero=PAG-0001", FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task CobroNuevo_DesdeElCliente_CancelarVuelveAlCliente()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), $"/cobros/nuevo?socioId={IdCliente}&returnUrl={Uri.EscapeDataString($"/clientes/{IdCliente}")}");

        Assert.Contains($"href=\"/clientes/{IdCliente}\" data-testid=\"cancelar\"", html);
    }

    [Fact]
    public async Task CobroNuevo_ErrorDeLaApi_SeMuestraSinRedirigir()
    {
        using var app = Configurar(new BlazorSsrFactory());
        app.Simular<ICobroApiClient>()
            .Setup(c => c.RegistrarPagoAsync(It.IsAny<RegistrarPagoInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<ResultadoPagoCliente>(false, "La fecha de registro no está permitida.", null));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), $"/cobros/nuevo?socioId={IdCliente}", "registrar-pago",
            new Dictionary<string, string>
            {
                ["PagoInput.SocioNegocioId"] = IdCliente.ToString(), ["PagoInput.ImporteTexto"] = "500.00",
                ["PagoInput.FechaRegistroTexto"] = "2026-09-28", ["PagoInput.CuentaCajaId"] = IdCaja.ToString(),
            });
        var html = HtmlSsr.Decodificar(await respuesta.Content.ReadAsStringAsync());

        Assert.Equal(System.Net.HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("La fecha de registro no está permitida.", html);
    }

    [Fact]
    public async Task CobroNuevo_SinCanModify_NoMuestraFormulario_YPostForzadoRecibeLaApi()
    {
        using var app = Configurar(new BlazorSsrFactory());
        app.Simular<ICobroApiClient>()
            .Setup(c => c.RegistrarPagoAsync(It.IsAny<RegistrarPagoInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<ResultadoPagoCliente>(false, "No tiene permisos para esta operación.", null));

        var html = await HtmlSsr.HtmlAsync(app.Cliente("Ejecutor"), $"/cobros/nuevo?socioId={IdCliente}");
        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente("Ejecutor"), $"/cobros/nuevo?socioId={IdCliente}", "registrar-pago",
            new Dictionary<string, string>
            {
                ["PagoInput.SocioNegocioId"] = IdCliente.ToString(), ["PagoInput.ImporteTexto"] = "500.00",
                ["PagoInput.FechaRegistroTexto"] = "2026-09-28", ["PagoInput.CuentaCajaId"] = IdCaja.ToString(),
            });
        var tras = HtmlSsr.Decodificar(await respuesta.Content.ReadAsStringAsync());

        Assert.Contains("No tiene permiso para realizar esta acción.", html);
        Assert.DoesNotContain("data-testid=\"guardar\"", html);
        Assert.Contains("No tiene permisos para esta operación.", tras);
    }

    [Fact]
    public async Task Cobros_SinFormularioDeAltaEnLinea_ConNuevoCobro()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), $"/cobros?socioId={IdCliente}");

        Assert.DoesNotContain("id=\"registrar-pago\"", html);
        Assert.Contains("data-testid=\"page-toolbar\"", html);
        Assert.Contains($"href=\"/cobros/nuevo?socioId={IdCliente}\"", html);
        Assert.Contains("id=\"aplicar-pago\"", html);
    }

    private static BlazorSsrFactory Configurar(BlazorSsrFactory app, bool cargaLenta = false)
    {
        var cliente = new SocioNegocioResponse { Id = IdCliente, Codigo = "C0001", NombreComercial = "Comercial Uno" };
        var socios = app.Simular<ISocioNegocioApiClient>();
        var cuentas = app.Simular<ICuentaContableApiClient>();
        var pagina = new PagedResult<SocioNegocioResponse>([cliente], 1, 50, 1);
        var paginaCuentas = new PagedResult<CuentaContableResponse>([new CuentaContableResponse { Id = IdCaja, Numero = "1101", Nombre = "Caja", TipoCuenta = TipoCuentaContable.Posteo, PosteoDirecto = true }], 1, 200, 1);
        if (cargaLenta)
        {
            // Respuestas asíncronas de verdad (como la API real): la página se pinta antes de que terminen las cargas.
            socios.Setup(c => c.ListAsync(It.IsAny<SocioNegocioSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
                .Returns(async () => { await Task.Delay(20); return pagina; });
            socios.Setup(c => c.GetByIdAsync(IdCliente, It.IsAny<CancellationToken>()))
                .Returns(async () => { await Task.Delay(20); return cliente; });
            cuentas.Setup(c => c.ListAsync(It.IsAny<CuentaContableSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
                .Returns(async () => { await Task.Delay(20); return paginaCuentas; });
        }
        else
        {
            socios.Setup(c => c.ListAsync(It.IsAny<SocioNegocioSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>())).ReturnsAsync(pagina);
            socios.Setup(c => c.GetByIdAsync(IdCliente, It.IsAny<CancellationToken>())).ReturnsAsync(cliente);
            cuentas.Setup(c => c.ListAsync(It.IsAny<CuentaContableSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>())).ReturnsAsync(paginaCuentas);
        }

        var cobros = app.Simular<ICobroApiClient>();
        cobros.Setup(c => c.ListMovimientosAbiertosAsync(IdCliente, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        app.Simular<ITerminoPagoApiClient>();
        return app;
    }
}
