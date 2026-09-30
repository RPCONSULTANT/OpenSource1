extern alias BlazorApp;
using System.Net;
using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.FacturasVenta.Calculo;
using OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Dtos;
using OpenSource1.Application.Features.NotasCreditoVenta.Posteo;
using OpenSource1.Application.Features.Series.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Borrador de nota de crédito en una sola página (spec no-series, Parte 4), Posteada en solo lectura, `/editar` y alta con series.</summary>
public sealed class DocumentoNotaBorradorTests
{
    private static readonly Guid IdAbierta = Guid.Parse("7d0c9a51-0000-0000-0000-0000000006b1");
    private static readonly Guid IdPosteada = Guid.Parse("7d0c9a51-0000-0000-0000-0000000006b2");
    private static readonly Guid IdSerieNc = Guid.Parse("7d0c9a51-0000-0000-0000-0000000006b3");
    private static readonly Guid IdSerieOtra = Guid.Parse("7d0c9a51-0000-0000-0000-0000000006b4");
    private static string Url(Guid id) => $"/notas-credito-venta/borradores/{id}";

    [Fact]
    public async Task PaginaUnica_CabeceraEditable_ConSerieYProximo_TablaYTotales()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), Url(IdAbierta));

        Assert.Contains("name=\"_handler\" value=\"guardar-cabecera-nota\"", html);
        Assert.Contains("name=\"UpdateInput.SerieRegistroId\"", html);
        Assert.Contains("Próximo número: <strong>NC-000004</strong>", html);
        Assert.Contains("data-testid=\"tabla-lineas\"", html);
        Assert.Contains("data-testid=\"totales\"", html);
        Assert.Contains($"href=\"{Url(IdAbierta)}?postear=true\"", html);
        Assert.Contains($"href=\"{Url(IdAbierta)}?eliminar=true\"", html);
        Assert.DoesNotContain("/editar", html);
    }

    [Fact]
    public async Task GuardarCabecera_EnviaFechasDescripcionYSerie()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var api = app.Simular<INotaCreditoVentaApiClient>();
        api.Setup(c => c.UpdateBorradorAsync(IdAbierta, It.Is<NotaCreditoCabeceraInput>(i => i.SerieRegistroId == IdSerieOtra && i.Descripcion == "Devolución"), 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<NotaCreditoVentaBorradorResponse>(true, "ok"));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), Url(IdAbierta), "guardar-cabecera-nota", new Dictionary<string, string>
        {
            ["UpdateInput.Xmin"] = "5", ["UpdateInput.FechaRegistroTexto"] = "2026-09-02", ["UpdateInput.FechaDocumentoTexto"] = "2026-09-03",
            ["UpdateInput.Descripcion"] = "Devolución", ["UpdateInput.SerieRegistroId"] = IdSerieOtra.ToString(),
        });

        Assert.Equal($"{Url(IdAbierta)}?ok=modificado", FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task Posteada_SoloLectura_EnlazaLaNota_YPostObsoletoMuestraElConflicto()
    {
        using var app = Configurar(new BlazorSsrFactory());
        app.Simular<INotaCreditoVentaApiClient>()
            .Setup(c => c.UpdateBorradorAsync(IdPosteada, It.IsAny<NotaCreditoCabeceraInput>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<NotaCreditoVentaBorradorResponse>(false, "El borrador de nota de crédito ya se posteó: es de solo lectura (abra su nota)."));

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), Url(IdPosteada));
        var obsoleto = await FormulariosSsr.EnviarAsync(app.Cliente(), Url(IdPosteada), "guardar-cabecera-nota", new Dictionary<string, string>
        {
            ["UpdateInput.Xmin"] = "5", ["UpdateInput.FechaRegistroTexto"] = "2026-09-02", ["UpdateInput.FechaDocumentoTexto"] = "2026-09-02",
        });

        Assert.Contains("data-testid=\"documento-cabecera\"", html);
        Assert.Contains("href=\"/notas-credito-venta/NC-000003\"", html);
        Assert.DoesNotContain("data-testid=\"guardar-cabecera\"", html);
        Assert.Contains("value=\"guardar-cabecera-nota\"", html);
        Assert.Contains("ya se posteó", HtmlSsr.Decodificar(await obsoleto.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Postear_VaALaNota_ConAviso()
    {
        using var app = Configurar(new BlazorSsrFactory());
        app.Simular<INotaCreditoVentaApiClient>()
            .Setup(c => c.PostearAsync(IdAbierta, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<ResultadoPosteoNotaCredito>(true, "ok", new ResultadoPosteoNotaCredito("NC-000004", 50m, 50m, "00000007", "Aviso de NC")));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), $"{Url(IdAbierta)}?postear=true", "confirm-postear", new Dictionary<string, string>());

        Assert.StartsWith("/notas-credito-venta/NC-000004?ok=posteada&aplicado=50.00&registro=00000007&aviso=", FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task RutaEditar_Redirige_YListadoConFiltroPosteada()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var api = app.Simular<INotaCreditoVentaApiClient>();

        var respuesta = await app.Cliente().GetAsync($"{Url(IdAbierta)}/editar");
        var lista = await HtmlSsr.HtmlAsync(app.Cliente(), "/notas-credito-venta/borradores?estado=3");

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(Url(IdAbierta), FormulariosSsr.Destino(respuesta));
        Assert.Contains("<option value=\"3\" selected", lista);
        api.Verify(c => c.ListBorradoresAsync(It.Is<NotaCreditoVentaBorradorFiltro?>(f => f!.Estado == 3), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    private static NotaCreditoVentaBorradorResponse Nota(Guid id, EstadoNotaCreditoBorrador estado, string? nota) => new()
    {
        Id = id, Numero = "NCB-000009", FacturaVentaNumero = "FV-000001", FacturaImporteTotal = 118m, Estado = estado, NotaCreditoVentaNumero = nota,
        NombreFacturacion = "Comercial Uno", FechaRegistro = new DateOnly(2026, 9, 1), FechaDocumento = new DateOnly(2026, 9, 1),
        Descripcion = "Vieja", Moneda = "DOP", SerieRegistroId = IdSerieNc, SerieRegistroCodigo = "NC", SerieBorradorCodigo = "NC-BORR", Xmin = 5,
    };

    private static BlazorSsrFactory Configurar(BlazorSsrFactory app)
    {
        var notas = app.Simular<INotaCreditoVentaApiClient>();
        notas.Setup(c => c.GetBorradorAsync(IdAbierta, It.IsAny<CancellationToken>())).ReturnsAsync(Nota(IdAbierta, EstadoNotaCreditoBorrador.Abierta, null));
        notas.Setup(c => c.GetBorradorAsync(IdPosteada, It.IsAny<CancellationToken>())).ReturnsAsync(Nota(IdPosteada, EstadoNotaCreditoBorrador.Posteada, "NC-000003"));
        notas.Setup(c => c.ListLineasAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        notas.Setup(c => c.ListLineasAcreditablesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        notas.Setup(c => c.GetTotalesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new TotalesFactura([], 0m, 0m, 0m));
        notas.Setup(c => c.ListBorradoresAsync(It.IsAny<NotaCreditoVentaBorradorFiltro?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<NotaCreditoVentaBorradorResponse>([], 1, 50, 0));

        var series = app.Simular<ISerieApiClient>();
        series.Setup(c => c.ActivasDelTipoAsync(It.IsAny<TipoDocumentoSerie>(), It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new SerieResponse { Id = IdSerieNc, Codigo = "NC", Descripcion = "Notas", TipoDocumento = TipoDocumentoSerie.NotaCreditoVenta, Activa = true },
            new SerieResponse { Id = IdSerieOtra, Codigo = "NC2", Descripcion = "Sucursal", TipoDocumento = TipoDocumentoSerie.NotaCreditoVenta, Activa = true },
        ]);
        series.Setup(c => c.ProximoAsync(IdSerieNc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<ProximoNumeroResponse>(true, "ok", new ProximoNumeroResponse("NC-000004", null)));
        return app;
    }
}
