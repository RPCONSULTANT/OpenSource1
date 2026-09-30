extern alias BlazorApp;
using BlazorApp::OpenSource1.Blazor.Components;
using BlazorApp::OpenSource1.Blazor.Components.Documento;
using BlazorApp::OpenSource1.Blazor.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenSource1.Application.Features.FacturasVenta.Calculo;
using OpenSource1.Application.Features.Series.Dtos;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>
/// Componentes compartidos del documento (spec no-series, Parte 4; S11): cabecera y totales en solo lectura, información de la
/// serie de registro, selector de series (conserva la serie vigente aunque no esté entre las activas, F7) y las cargas de
/// <see cref="SeriesOpciones"/> que no rompen la página.
/// </summary>
public sealed class ComponentesDocumentoTests
{
    [Fact]
    public async Task Totales_SubtotalDescuentosItbisYTotal_ConLosGruposDeIva()
    {
        var totales = TotalesDocumento.Desde(
            new TotalesFactura([new GrupoIvaCalculado("ITBIS18", 18m, 95m, 17.10m)], 95m, 17.10m, 112.10m), descuentos: 5m);

        var html = await Renderizar<DocumentoTotales>(new() { ["Totales"] = totales });

        Assert.Contains("data-testid=\"totales\"", html);
        Assert.Contains("<dd class=\"font-semibold text-slate-800\" data-testid=\"total-subtotal\">100.00</dd>", html);
        Assert.Contains("data-testid=\"total-descuentos\">5.00<", html);
        Assert.Contains("data-testid=\"total-sin-iva\">95.00<", html);
        Assert.Contains("data-testid=\"total-iva\">17.10<", html);
        Assert.Contains(">112.10<", html);
        Assert.Contains("ITBIS18", html);
    }

    [Fact]
    public async Task Totales_SinTotales_MensajeYTitulo()
    {
        var html = await Renderizar<DocumentoTotales>(new() { ["Totales"] = null, ["Titulo"] = "Totales (vista previa)" });

        Assert.Contains("Totales (vista previa)", html);
        Assert.Contains("No fue posible calcular los totales.", html);
        Assert.DoesNotContain("data-testid=\"total\"", html);
    }

    [Fact]
    public async Task Cabecera_CamposConEnlaceYDetalle()
    {
        var html = await Renderizar<DocumentoCabecera>(new()
        {
            ["Campos"] = new List<CampoDocumento> { new("Vender a", "C-001 — Comercial", "/clientes/1", "RNC 1"), new("Descripción", "Texto", Ancho: true) },
        });

        Assert.Contains("data-testid=\"documento-cabecera\"", html);
        Assert.Contains("href=\"/clientes/1\"", html);
        Assert.Contains("RNC 1", html);
        Assert.Contains("Vender a", html);
        Assert.Contains("sm:col-span-2 lg:col-span-4", html);
    }

    [Fact]
    public async Task InfoDeSerie_MuestraProximoOAvisoOError()
    {
        var conProximo = await Renderizar<SerieNumeracionInfo>(new() { ["Codigo"] = "FV", ["ProximoNumero"] = "00000012", ["Aviso"] = "Quedan 3." });
        var conError = await Renderizar<SerieNumeracionInfo>(new() { ["Codigo"] = "FV", ["Error"] = "La serie 'FV' está inactiva." });

        Assert.Contains("Próximo número: <strong>00000012</strong>", conProximo);
        Assert.Contains("Quedan 3.", conProximo);
        Assert.Contains("La serie 'FV' está inactiva.", conError);
        Assert.DoesNotContain("Próximo número", conError);
    }

    [Fact]
    public async Task Selector_ConservaLaSerieVigenteAunqueNoEsteEntreLasActivas()
    {
        var vigente = Guid.NewGuid();
        var otra = Serie("FV2", "Facturas 2", "FV2-0001");

        var html = await Renderizar<SelectorSerie>(new()
        {
            ["Id"] = "serie-registro", ["Name"] = "Input.SerieRegistroId", ["Series"] = new List<SerieResponse> { otra },
            ["Seleccion"] = vigente, ["VigenteId"] = vigente, ["VigenteCodigo"] = "FV",
        });
        var sinCarga = await Renderizar<SelectorSerie>(new()
        {
            ["Id"] = "serie-registro", ["Name"] = "Input.SerieRegistroId", ["Series"] = new List<SerieResponse>(),
            ["Seleccion"] = vigente, ["VigenteId"] = vigente, ["VigenteCodigo"] = "FV",
        });

        Assert.Contains("name=\"Input.SerieRegistroId\"", html);
        Assert.Contains($"<option value=\"{vigente}\" selected=\"selected\">FV (no disponible)</option>", html);
        Assert.Contains($"<option value=\"{otra.Id}\">FV2 — Facturas 2 (próximo FV2-0001)</option>", html);
        Assert.Contains($"<option value=\"{vigente}\" selected=\"selected\">FV (no disponible)</option>", sinCarga);
    }

    [Fact]
    public async Task Selector_OpcionConfiguradaSeleccionadaSinSeleccion()
    {
        var serie = Serie("NC", "Notas", null);

        var html = await Renderizar<SelectorSerie>(new()
        {
            ["Id"] = "serie-borrador", ["Name"] = "AddInput.SerieBorradorId", ["Series"] = new List<SerieResponse> { serie }, ["OpcionConfigurada"] = true,
        });

        // El renderizador emite value="" y un bool true como atributos sin valor (el POST envía "" = la configurada).
        Assert.Contains("<option value selected>— La configurada —</option>", html);
        Assert.Contains($"<option value=\"{serie.Id}\">NC — Notas</option>", html);
    }

    [Fact]
    public async Task SeriesOpciones_FalloDeCarga_ListaVaciaConCargaFallida_YProximoConError()
    {
        var cliente = new Mock<ISerieApiClient>();
        cliente.Setup(c => c.ActivasDelTipoAsync(TipoDocumentoSerie.FacturaVenta, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("caída"));
        cliente.Setup(c => c.ProximoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<ProximoNumeroResponse>(false, "La serie 'FV' está inactiva."));

        var series = await SeriesOpciones.ActivasAsync(cliente.Object, TipoDocumentoSerie.FacturaVenta, NullLogger.Instance);
        var proximo = await SeriesOpciones.ProximoAsync(cliente.Object, Guid.NewGuid(), NullLogger.Instance);

        Assert.True(series.CargaFallida);
        Assert.Empty(series.Items);
        Assert.Equal(new ProximoVista(null, null, "La serie 'FV' está inactiva."), proximo);
    }

    [Fact]
    public async Task SeriesOpciones_ProximoConAviso()
    {
        var cliente = new Mock<ISerieApiClient>();
        cliente.Setup(c => c.ProximoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<ProximoNumeroResponse>(true, "ok", new ProximoNumeroResponse("FV-000012", "Quedan 3.")));
        var roto = new Mock<ISerieApiClient>();
        roto.Setup(c => c.ProximoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("caída"));

        Assert.Equal(new ProximoVista("FV-000012", "Quedan 3.", null), await SeriesOpciones.ProximoAsync(cliente.Object, Guid.NewGuid(), NullLogger.Instance));
        Assert.Equal("No fue posible calcular el próximo número.", (await SeriesOpciones.ProximoAsync(roto.Object, Guid.NewGuid(), NullLogger.Instance)).Error);

        // Un 400 trae el motivo real en Errors ("Revise los datos:" en Message): la vista muestra el motivo.
        var invalida = new Mock<ISerieApiClient>();
        invalida.Setup(c => c.ProximoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<ProximoNumeroResponse>(false, "Revise los datos:", Errors: ["No hay una línea vigente de la serie 'FV'."]));
        Assert.Equal("No hay una línea vigente de la serie 'FV'.", (await SeriesOpciones.ProximoAsync(invalida.Object, Guid.NewGuid(), NullLogger.Instance)).Error);
    }

    [Fact]
    public void Estados_IncluyenPosteada()
    {
        Assert.Equal("Posteada", VentasOpciones.Estado(EstadoFacturaBorrador.Posteada));
        Assert.Equal("Abierta", VentasOpciones.EstadoNota(EstadoNotaCreditoBorrador.Abierta));
        Assert.Equal("Posteada", VentasOpciones.EstadoNota(EstadoNotaCreditoBorrador.Posteada));
    }

    private static SerieResponse Serie(string codigo, string descripcion, string? proximo) => new()
    {
        Id = Guid.NewGuid(), Codigo = codigo, Descripcion = descripcion, TipoDocumento = TipoDocumentoSerie.FacturaVenta, Activa = true, ProximoNumero = proximo,
    };

    private static async Task<string> Renderizar<T>(Dictionary<string, object?> parametros) where T : Microsoft.AspNetCore.Components.IComponent =>
        HtmlSsr.Decodificar(await RenderizadorComponentes.RenderizarAsync<T>(PermisosTestAuthHandler.Principal("Administrador"), parametros));
}
