extern alias BlazorApp;

using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.Almacenes.Dtos;
using OpenSource1.Application.Features.FacturasVenta.Borradores.Dtos;
using OpenSource1.Application.Features.FacturasVenta.Posteadas;
using OpenSource1.Application.Features.FacturasVenta.Posteadas.Dtos;
using OpenSource1.Application.Features.MovimientosCliente.Dtos;
using OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Dtos;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Application.Features.TerminosPago.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Fix-Features C3: facturas filtradas por cliente, nueva factura con cliente precargado y nota de crédito por cliente.</summary>
public sealed class AccionesFacturasTests
{
    private static readonly Guid IdCliente = Guid.Parse("7f000000-0000-0000-0000-000000000001");
    private static readonly Guid IdBorradorAbierto = Guid.Parse("7f000000-0000-0000-0000-0000000000b1");
    private static readonly Guid IdBorradorLiberado = Guid.Parse("7f000000-0000-0000-0000-0000000000b2");
    private static readonly Guid IdNota = Guid.Parse("7f000000-0000-0000-0000-0000000000c1");

    [Fact]
    public async Task Facturas_FiltranPorCliente_YBarraConNuevaFacturaYAccionesDeLaSeleccion()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var api = app.Simular<IFacturaVentaApiClient>();

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), $"/facturas-venta?socioId={IdCliente}&sel=FV0001");

        api.Verify(c => c.ListFacturasAsync(It.Is<FacturaVentaSearchCriteria?>(f => f!.SocioNegocioId == IdCliente), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        Assert.Contains("data-testid=\"page-toolbar\"", html);
        Assert.Contains("href=\"/facturas-venta/nueva", html);
        Assert.Contains("href=\"/facturas-venta/FV0001?crearNota=true\"", html);
        Assert.Contains($"href=\"/ventas/movimientos-cliente?socioId={IdCliente}\"", html);
        Assert.Contains("C0001 — Comercial Uno", html);
        Assert.Contains($"<input type=\"hidden\" name=\"socioId\" value=\"{IdCliente}\"", html);
        // La selección y la paginación conservan el cliente filtrado.
        Assert.Contains($"href=\"/facturas-venta?socioId={IdCliente}&sel=FV0001\"", html);
    }

    [Fact]
    public async Task Facturas_SeleccionQueNoEstaEnLaPagina_DeshabilitaLasAcciones()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), "/facturas-venta?sel=FV9999");

        Assert.DoesNotContain("href=\"/facturas-venta/FV9999?crearNota=true\"", html);
        Assert.Contains("aria-disabled=\"true\"", html);
    }

    [Fact]
    public async Task FacturaDetalle_TieneBarraConNotaDeCreditoYMovimientos()
    {
        using var app = Configurar(new BlazorSsrFactory());
        app.Simular<IFacturaVentaApiClient>()
            .Setup(c => c.GetFacturaAsync("FV0001", It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Yield();
                return new FacturaVentaDetalleResponse(
                    new FacturaVentaResponse { Numero = "FV0001", SocioNegocioId = IdCliente, NombreFacturacion = "Comercial Uno" }, [], []);
            });

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), "/facturas-venta/FV0001");

        Assert.Contains("data-testid=\"page-toolbar\"", html);
        Assert.Contains("href=\"/facturas-venta/FV0001?crearNota=true\"", html);
        Assert.Contains($"href=\"/ventas/movimientos-cliente?socioId={IdCliente}\"", html);
    }

    [Fact]
    public async Task NuevaFactura_ConSocio_PrecargaYBloqueaElCliente()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), $"/facturas-venta/nueva?socioId={IdCliente}");

        Assert.Contains("data-testid=\"cliente-precargado\"", html);
        Assert.Contains($"<input type=\"hidden\" name=\"AddInput.SocioNegocioId\" value=\"{IdCliente}\"", html);
        Assert.Contains("CONTADO — Contado", html);
        Assert.Contains("GENERAL", html);
        Assert.Contains("href=\"/facturas-venta/nueva\"", html);
        // Cliente bloqueado: ni selector "Vender a" ni buscador de clientes.
        Assert.DoesNotContain("<select id=\"AddInput_SocioNegocioId\"", html);
        Assert.DoesNotContain("data-testid=\"buscar-socio\"", html);
        Assert.Contains("name=\"AddInput.SocioNegocioFacturarAId\"", html);
    }

    [Fact]
    public async Task NuevaFactura_SocioInexistente_AvisoYSelectorNormal()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), $"/facturas-venta/nueva?socioId={Guid.NewGuid()}");

        Assert.Contains("No se encontró el cliente indicado", html);
        Assert.DoesNotContain("data-testid=\"cliente-precargado\"", html);
        Assert.Contains("name=\"AddInput.SocioNegocioId\"", html);
        Assert.Contains("data-testid=\"buscar-socio\"", html);
    }

    [Fact]
    public async Task NuevaFactura_Guardar_CreaElBorradorConElCliente()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var borrador = Guid.NewGuid();
        app.Simular<IFacturaVentaApiClient>()
            .Setup(c => c.CreateBorradorAsync(It.Is<BorradorCabeceraInput>(i => i.SocioNegocioId == IdCliente), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<FacturaVentaBorradorResponse>(true, "ok", new FacturaVentaBorradorResponse { Id = borrador }));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), $"/facturas-venta/nueva?socioId={IdCliente}", "add-borrador",
            new Dictionary<string, string> { ["AddInput.SocioNegocioId"] = IdCliente.ToString(), ["AddInput.FechaRegistroTexto"] = "2026-09-28" });

        Assert.Equal($"/facturas-venta/borradores/{borrador}?ok=creado", FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task NotaCreditoNueva_ListaSoloFacturasConPendiente_YCreaElBorrador()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var nota = Guid.NewGuid();
        app.Simular<INotaCreditoVentaApiClient>()
            .Setup(c => c.CreateBorradorAsync(It.Is<NotaCreditoBorradorInput>(i => i.FacturaVentaNumero == "FV0001" && i.CopiarLineas), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<NotaCreditoVentaBorradorResponse>(true, "ok", new NotaCreditoVentaBorradorResponse { Id = nota }));

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), $"/notas-credito-venta/nueva?socioId={IdCliente}");
        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), $"/notas-credito-venta/nueva?socioId={IdCliente}", "nueva-nota-socio",
            new Dictionary<string, string> { ["NotaInput.FacturaVentaNumero"] = "FV0001", ["NotaInput.CopiarLineas"] = "true" });

        Assert.Contains("C0001 — Comercial Uno", html);
        Assert.Contains("value=\"FV0001\"", html);
        Assert.DoesNotContain("value=\"PAG-0001\"", html);
        Assert.DoesNotContain("value=\"FV0002\"", html);
        Assert.Equal($"/notas-credito-venta/borradores/{nota}?ok=creado", FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task NotaCreditoNueva_SinSocio_ListaClientesParaElegir()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), "/notas-credito-venta/nueva");

        Assert.Contains("data-testid=\"elegir-cliente\"", html);
        Assert.Contains($"href=\"/notas-credito-venta/nueva?socioId={IdCliente}\"", html);
    }

    [Fact]
    public async Task Borradores_ConNuevaFacturaYVerCliente()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var facturas = await HtmlSsr.HtmlAsync(app.Cliente(), "/facturas-venta/borradores");
        var notas = await HtmlSsr.HtmlAsync(app.Cliente(), "/notas-credito-venta/borradores");

        Assert.Contains("href=\"/facturas-venta/nueva?returnUrl=", facturas);
        Assert.Contains("data-testid=\"menu-ver\"", facturas);
        Assert.Contains("href=\"/notas-credito-venta/nueva", notas);
        Assert.Contains("data-testid=\"menu-ver\"", notas);
    }

    [Fact]
    public async Task BorradoresFactura_Liberado_SeleccionableParaVer_SinEditarNiEliminar()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var liberado = await HtmlSsr.HtmlAsync(app.Cliente(), $"/facturas-venta/borradores?sel={IdBorradorLiberado}");
        var abierto = await HtmlSsr.HtmlAsync(app.Cliente(), $"/facturas-venta/borradores?sel={IdBorradorAbierto}");
        var sinSeleccion = await HtmlSsr.HtmlAsync(app.Cliente(), "/facturas-venta/borradores");

        Assert.Contains($"href=\"/facturas-venta/borradores/{IdBorradorLiberado}\"", liberado);
        Assert.Contains($"href=\"/clientes/{IdCliente}\"", liberado);
        Assert.DoesNotContain("data-testid=\"accion-editar\"", liberado);
        Assert.DoesNotContain("data-testid=\"accion-eliminar\"", liberado);

        Assert.Contains($"<a data-testid=\"accion-editar\" href=\"/facturas-venta/borradores/{IdBorradorAbierto}/editar?returnUrl=", abierto);
        Assert.Contains($"href=\"/clientes/{IdCliente}\"", abierto);

        Assert.Contains("<span data-testid=\"accion-editar\" aria-disabled=\"true\"", sinSeleccion);
        Assert.Contains("<span data-testid=\"accion-eliminar\" aria-disabled=\"true\"", sinSeleccion);
    }

    [Fact]
    public async Task BorradoresNota_SeleccionHabilitaVerCliente()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), $"/notas-credito-venta/borradores?sel={IdNota}");

        Assert.Contains($"href=\"/notas-credito-venta/borradores/{IdNota}\"", html);
        Assert.Contains($"href=\"/clientes/{IdCliente}\"", html);
        Assert.Contains("href=\"/notas-credito-venta/nueva?returnUrl=", html);
    }

    [Theory]
    [InlineData("/facturas-venta/borradores/{0}", "/facturas-venta/borradores/{0}/editar?returnUrl=%2Ffacturas-venta%2Fborradores%2F{0}")]
    [InlineData("/notas-credito-venta/borradores/{0}", "/notas-credito-venta/borradores/{0}/editar?returnUrl=%2Fnotas-credito-venta%2Fborradores%2F{0}")]
    public async Task EditorBorrador_ModificarCabecera_VaALaRutaNuevaConRetorno(string ruta, string esperado)
    {
        using var app = Configurar(new BlazorSsrFactory());
        var id = ruta.StartsWith("/facturas", StringComparison.Ordinal) ? IdBorradorAbierto : IdNota;

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), string.Format(System.Globalization.CultureInfo.InvariantCulture, ruta, id));

        Assert.Contains($"href=\"{string.Format(System.Globalization.CultureInfo.InvariantCulture, esperado, id)}\"", html);
        Assert.DoesNotContain("editId=", html);
    }

    private static BlazorSsrFactory Configurar(BlazorSsrFactory app)
    {
        var terminoId = Guid.NewGuid();
        var cliente = new SocioNegocioResponse { Id = IdCliente, Codigo = "C0001", NombreComercial = "Comercial Uno", TerminoPagoId = terminoId, GrupoClienteContableCodigo = "GENERAL" };
        var socios = app.Simular<ISocioNegocioApiClient>();
        // Respuesta asíncrona de verdad (lección de la puerta B: el primer render ocurre antes de que termine la carga).
        socios.Setup(c => c.GetByIdAsync(IdCliente, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Yield();
                return cliente;
            });
        socios.Setup(c => c.ListAsync(It.IsAny<SocioNegocioSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<SocioNegocioResponse>([cliente], 1, 50, 1));
        app.Simular<ITerminoPagoApiClient>().Setup(c => c.GetByIdAsync(terminoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TerminoPagoResponse { Id = terminoId, Codigo = "CONTADO", Descripcion = "Contado" });

        var facturas = app.Simular<IFacturaVentaApiClient>();
        facturas.Setup(c => c.ListFacturasAsync(It.IsAny<FacturaVentaSearchCriteria?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Yield();
                return new PagedResult<FacturaVentaResponse>([new FacturaVentaResponse { Numero = "FV0001", SocioNegocioId = IdCliente, NombreFacturacion = "Comercial Uno" }], 1, 50, 1);
            });
        facturas.Setup(c => c.ListBorradoresAsync(It.IsAny<FacturaVentaBorradorFiltro?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<FacturaVentaBorradorResponse>(
            [
                new FacturaVentaBorradorResponse { Id = IdBorradorAbierto, Numero = "B-1", Estado = EstadoFacturaBorrador.Abierta, SocioNegocioId = IdCliente },
                new FacturaVentaBorradorResponse { Id = IdBorradorLiberado, Numero = "B-2", Estado = EstadoFacturaBorrador.Liberada, SocioNegocioId = IdCliente },
            ], 1, 50, 2));
        facturas.Setup(c => c.GetBorradorAsync(IdBorradorAbierto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacturaVentaBorradorResponse { Id = IdBorradorAbierto, Numero = "B-1", Estado = EstadoFacturaBorrador.Abierta, SocioNegocioId = IdCliente });

        var notas = app.Simular<INotaCreditoVentaApiClient>();
        notas.Setup(c => c.ListBorradoresAsync(It.IsAny<NotaCreditoVentaBorradorFiltro?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<NotaCreditoVentaBorradorResponse>(
                [new NotaCreditoVentaBorradorResponse { Id = IdNota, Numero = "NC-1", FacturaVentaNumero = "FV0001", SocioNegocioId = IdCliente }], 1, 50, 1));
        notas.Setup(c => c.GetBorradorAsync(IdNota, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotaCreditoVentaBorradorResponse { Id = IdNota, Numero = "NC-1", FacturaVentaNumero = "FV0001", SocioNegocioId = IdCliente });

        app.Simular<ICobroApiClient>().Setup(c => c.ListMovimientosAbiertosAsync(IdCliente, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Yield();
                return new List<MovimientoClienteResponse>
                {
                    new() { Id = 1, TipoDocumento = TipoDocumentoCliente.Factura, NumeroDocumento = "FV0001", ImporteOriginal = 100m, ImporteRestante = 60m, Abierta = true },
                    new() { Id = 2, TipoDocumento = TipoDocumentoCliente.Pago, NumeroDocumento = "PAG-0001", ImporteOriginal = -40m, ImporteRestante = -40m, Abierta = true },
                    new() { Id = 3, TipoDocumento = TipoDocumentoCliente.Factura, NumeroDocumento = "FV0002", ImporteOriginal = 50m, ImporteRestante = 0m, Abierta = false },
                };
            });

        // R2: con un almacén para que VentasOpciones.AlmacenesAsync cargue.
        app.Simular<IAlmacenApiClient>()
            .Setup(c => c.ListAsync(It.IsAny<AlmacenSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<AlmacenResponse>([new AlmacenResponse { Id = Guid.NewGuid(), Codigo = "PRINC", Nombre = "Principal" }], 1, 50, 1));
        return app;
    }
}
