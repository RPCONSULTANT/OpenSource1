extern alias BlazorApp;

using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.FacturasVenta.Copia;
using OpenSource1.Application.Features.FacturasVenta.Posteadas;
using OpenSource1.Application.Features.FacturasVenta.Posteadas.Dtos;
using OpenSource1.Application.Features.NotasCreditoVenta.Posteadas;
using OpenSource1.Application.Features.NotasCreditoVenta.Posteadas.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Factura y nota posteadas con el diseño del documento (solo lectura), Copiar a borrador y enlaces al borrador (spec no-series).</summary>
public sealed class DocumentosPosteadosTests
{
    private static readonly Guid IdBorrador = Guid.Parse("7d0c9a51-0000-0000-0000-0000000007c1");
    private static readonly Guid IdNuevo = Guid.Parse("7d0c9a51-0000-0000-0000-0000000007c2");

    [Fact]
    public async Task Factura_DocumentoSoloLectura_EnlaceAlBorrador_YCrearCopiarABorrador()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), "/facturas-venta/FV-000009?ok=posteada&aviso=Quedan%203");

        Assert.Contains("data-testid=\"cabecera-factura\"", html);
        Assert.Contains("data-testid=\"documento-cabecera\"", html);
        Assert.Contains("Vender a", html);
        Assert.Contains("C-001 — Comercial Uno", html);
        Assert.Contains("data-testid=\"lineas-documento\"", html);
        Assert.Contains("La factura no tiene líneas.", html);
        Assert.Contains("data-testid=\"totales\"", html);
        Assert.Contains($"href=\"/facturas-venta/borradores/{IdBorrador}\"", html);
        Assert.Contains("href=\"/facturas-venta/FV-000009?copiar=true", html);
        Assert.Contains("data-testid=\"aviso-numeracion\"", html);
        Assert.Contains("Quedan 3", html);
        // El diálogo solo se abre con ?copiar=true; el formulario sigue en el árbol.
        Assert.DoesNotContain("Sí, copiar", html);
        Assert.Contains("value=\"copiar-borrador\"", html);
    }

    [Fact]
    public async Task Factura_AvisoDeNumeracion_SoloTrasElPosteo()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), "/facturas-venta/FV-000009?aviso=Quedan%203");

        Assert.DoesNotContain("data-testid=\"aviso-numeracion\"", html);
    }

    [Fact]
    public async Task Factura_SinBorradorDeOrigen_MuestraElNumeroSinEnlace()
    {
        using var app = Configurar(new BlazorSsrFactory(), conBorrador: false);

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), "/facturas-venta/FV-000009");

        Assert.Contains("00000077", html);
        Assert.DoesNotContain("href=\"/facturas-venta/borradores/", html.Replace("href=\"/facturas-venta/borradores\"", string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public async Task CopiarABorrador_Dialogo_ConConfirmacionYCancelar()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), "/facturas-venta/FV-000009?copiar=true");

        Assert.Contains("Copiar a borrador", html);
        Assert.Contains("Sí, copiar", html);
        Assert.Contains("value=\"copiar-borrador\"", html);
        Assert.Contains("href=\"/facturas-venta/FV-000009\"", html);
    }

    [Fact]
    public async Task CopiarABorrador_SinAvisos_AbreElBorrador()
    {
        using var app = Configurar(new BlazorSsrFactory());
        app.Simular<IFacturaVentaApiClient>()
            .Setup(c => c.CopiarABorradorAsync("FV-000009", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<CopiaFacturaResponse>(true, "ok", new CopiaFacturaResponse(IdNuevo, "00000078", [])));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), "/facturas-venta/FV-000009?copiar=true", "copiar-borrador", new Dictionary<string, string>());

        Assert.Equal($"/facturas-venta/borradores/{IdNuevo}?ok=copiado", FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task CopiarABorrador_ConAvisos_LosMuestraConElEnlace()
    {
        using var app = Configurar(new BlazorSsrFactory());
        app.Simular<IFacturaVentaApiClient>()
            .Setup(c => c.CopiarABorradorAsync("FV-000009", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<CopiaFacturaResponse>(true, "ok",
                new CopiaFacturaResponse(IdNuevo, "00000078", ["Línea 20000: El producto no existe o está bloqueado para la venta. La línea no se copió."])));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), "/facturas-venta/FV-000009?copiar=true", "copiar-borrador", new Dictionary<string, string>());
        var html = HtmlSsr.Decodificar(await respuesta.Content.ReadAsStringAsync());

        Assert.Equal(System.Net.HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("Línea 20000", html);
        Assert.Contains($"href=\"/facturas-venta/borradores/{IdNuevo}\"", html);
        Assert.Contains("data-testid=\"borrador-copiado\"", html);
        // Hecha la copia, el diálogo ya no se vuelve a ofrecer (el formulario sigue en el árbol).
        Assert.DoesNotContain("Sí, copiar", html);
    }

    [Fact]
    public async Task CopiarABorrador_Error_MuestraElMensajeDeLaApi()
    {
        using var app = Configurar(new BlazorSsrFactory());
        app.Simular<IFacturaVentaApiClient>()
            .Setup(c => c.CopiarABorradorAsync("FV-000009", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<CopiaFacturaResponse>(false, "Revise los datos:", Errors: ["El cliente C-001 está bloqueado."]));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), "/facturas-venta/FV-000009?copiar=true", "copiar-borrador", new Dictionary<string, string>());
        var html = HtmlSsr.Decodificar(await respuesta.Content.ReadAsStringAsync());

        Assert.Contains("Revise los datos:", html);
        Assert.Contains("El cliente C-001 está bloqueado.", html);
        Assert.DoesNotContain("data-testid=\"borrador-copiado\"", html);
    }

    [Fact]
    public async Task CopiarABorrador_SinCanAdd_NoSeOfrece_YElPostForzadoMuestraElMensajeDeLaApi()
    {
        using var app = Configurar(new BlazorSsrFactory());
        app.Simular<IFacturaVentaApiClient>()
            .Setup(c => c.CopiarABorradorAsync("FV-000009", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<CopiaFacturaResponse>(false, "No tiene permisos para crear borradores."));
        var soloConsulta = app.Cliente("Ejecutor", permisos: "CanConsult");

        var html = await HtmlSsr.HtmlAsync(soloConsulta, "/facturas-venta/FV-000009");
        var forzado = await FormulariosSsr.EnviarAsync(soloConsulta, "/facturas-venta/FV-000009", "copiar-borrador", new Dictionary<string, string>());

        Assert.DoesNotContain("?copiar=true", html);
        Assert.Contains("value=\"copiar-borrador\"", html);
        Assert.Contains("No tiene permisos para crear borradores.", HtmlSsr.Decodificar(await forzado.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task CrearNota_DialogoConSeries_EnviaLasSeriesElegidas()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var serieBorrador = Guid.NewGuid();
        var serieRegistro = Guid.NewGuid();
        var series = app.Simular<ISerieApiClient>();
        series.Setup(c => c.ActivasDelTipoAsync(OpenSource1.Core.Enums.TipoDocumentoSerie.BorradorNotaCreditoVenta, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OpenSource1.Application.Features.Series.Dtos.SerieResponse { Id = serieBorrador, Codigo = "NCB", Descripcion = "Borradores de nota" }]);
        series.Setup(c => c.ActivasDelTipoAsync(OpenSource1.Core.Enums.TipoDocumentoSerie.NotaCreditoVenta, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OpenSource1.Application.Features.Series.Dtos.SerieResponse { Id = serieRegistro, Codigo = "NC", Descripcion = "Notas de crédito" }]);
        var nota = Guid.NewGuid();
        var notas = app.Simular<INotaCreditoVentaApiClient>();
        notas.Setup(c => c.CreateBorradorAsync(It.IsAny<NotaCreditoBorradorInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Dtos.NotaCreditoVentaBorradorResponse>(
                true, "ok", new OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Dtos.NotaCreditoVentaBorradorResponse { Id = nota }));

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), "/facturas-venta/FV-000009?crearNota=true");
        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), "/facturas-venta/FV-000009?crearNota=true", "crear-nota",
            new Dictionary<string, string> { ["CrearNotaInput.SerieBorradorId"] = serieBorrador.ToString(), ["CrearNotaInput.SerieRegistroId"] = "" });

        Assert.Contains("name=\"CrearNotaInput.SerieBorradorId\"", html);
        Assert.Contains("name=\"CrearNotaInput.SerieRegistroId\"", html);
        Assert.Contains("— La configurada —", html);
        Assert.Contains("NC — Notas de crédito", html);
        Assert.Equal($"/notas-credito-venta/borradores/{nota}?ok=creado", FormulariosSsr.Destino(respuesta));
        notas.Verify(c => c.CreateBorradorAsync(
            It.Is<NotaCreditoBorradorInput>(i => i.FacturaVentaNumero == "FV-000009" && i.SerieBorradorId == serieBorrador && i.SerieRegistroId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Nota_DocumentoSoloLectura_ConEnlaceAlBorrador()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), "/notas-credito-venta/NC-000003?ok=posteada&aviso=Quedan%202");

        Assert.Contains("data-testid=\"cabecera-nota\"", html);
        Assert.Contains("data-testid=\"documento-cabecera\"", html);
        Assert.Contains("href=\"/facturas-venta/FV-000009\"", html);
        Assert.Contains("data-testid=\"lineas-documento\"", html);
        Assert.Contains("La nota de crédito no tiene líneas.", html);
        Assert.Contains("data-testid=\"totales\"", html);
        Assert.Contains($"href=\"/notas-credito-venta/borradores/{IdBorrador}\"", html);
        Assert.Contains("data-testid=\"aviso-numeracion\"", html);
        Assert.Contains("Quedan 2", html);
    }

    private static BlazorSsrFactory Configurar(BlazorSsrFactory app, bool conBorrador = true)
    {
        var cabecera = new FacturaVentaResponse
        {
            Numero = "FV-000009", NumeroBorrador = "00000077", FacturaVentaBorradorId = conBorrador ? IdBorrador : null,
            NombreFacturacion = "Comercial Uno", SocioNegocioCodigo = "C-001", SocioNegocioNombre = "Comercial Uno", SocioNegocioFacturarACodigo = "C-001",
            FechaRegistro = new DateOnly(2026, 9, 1), FechaDocumento = new DateOnly(2026, 9, 1), FechaVencimiento = new DateOnly(2026, 9, 30),
            AlmacenCodigo = "PRINC", Moneda = "DOP", ImporteSinIva = 100m, ImporteIva = 18m, ImporteTotal = 118m,
        };
        var facturas = app.Simular<IFacturaVentaApiClient>();
        facturas.Setup(c => c.GetFacturaAsync("FV-000009", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacturaVentaDetalleResponse(cabecera, [],
                [new LineaIvaFacturaVentaResponse { IdentificadorIva = "ITBIS18", PorcentajeIva = 18m, BaseImponible = 100m, ImporteIva = 18m }]));
        facturas.Setup(c => c.ListFacturasAsync(It.IsAny<FacturaVentaSearchCriteria?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<FacturaVentaResponse>([cabecera], 1, 50, 1));

        var notas = app.Simular<INotaCreditoVentaApiClient>();
        notas.Setup(c => c.ListNotasAsync(It.IsAny<NotaCreditoVentaSearchCriteria?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<NotaCreditoVentaResponse>([], 1, 50, 0));
        notas.Setup(c => c.GetNotaAsync("NC-000003", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotaCreditoVentaDetalleResponse(
                new NotaCreditoVentaResponse
                {
                    Numero = "NC-000003", NumeroBorrador = "NCB-1", NotaCreditoVentaBorradorId = IdBorrador, FacturaVentaNumero = "FV-000009",
                    NombreFacturacion = "Comercial Uno", SocioNegocioFacturarACodigo = "C-001", FechaRegistro = new DateOnly(2026, 9, 2),
                    FechaDocumento = new DateOnly(2026, 9, 2), Moneda = "DOP", ImporteSinIva = 50m, ImporteIva = 9m, ImporteTotal = 59m,
                },
                [],
                [new LineaIvaNotaCreditoVentaResponse { IdentificadorIva = "ITBIS18", PorcentajeIva = 18m, BaseImponible = 50m, ImporteIva = 9m }]));
        return app;
    }
}
