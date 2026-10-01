extern alias BlazorApp;
using System.Net;
using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.ConfiguracionNumeracion.Dtos;
using OpenSource1.Application.Features.Series.Dtos;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Configuración de numeración: fallo inesperado a mitad del guardado y carga paralela de las series de cada tipo.</summary>
public sealed class ConfiguracionNumeracionPaginaTests
{
    private const string Url = "/configuracion/numeracion";
    private static readonly Guid IdSerie = Guid.Parse("5e000000-0000-0000-0000-000000000011");
    private static readonly Guid IdOtra = Guid.Parse("5e000000-0000-0000-0000-000000000013");

    [Fact]
    public async Task Guardar_ExcepcionAMitad_ConservaLaEleccionDeTodasLasFilas_EInformaLasGuardadas()
    {
        using var app = new BlazorSsrFactory();
        SimularSeries(app);
        var guardada = false;
        var api = app.Simular<IConfiguracionNumeracionApiClient>();
        api.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            [
                guardada
                    ? Fila(TipoDocumentoSerie.FacturaVenta, IdOtra, "FV2", 9)
                    : Fila(TipoDocumentoSerie.FacturaVenta, IdSerie, "FV", 4),
                Fila(TipoDocumentoSerie.Cobro, IdSerie, "COBRO", 7),
                Fila(TipoDocumentoSerie.AsientoContable, IdSerie, "AS", 8),
            ]);
        api.Setup(c => c.UpdateAsync(TipoDocumentoSerie.FacturaVenta, IdOtra, 4, It.IsAny<CancellationToken>()))
            .Callback(() => guardada = true)
            .ReturnsAsync(new VentaOperationResult<ConfiguracionNumeracionResponse>(true, "ok"));
        api.Setup(c => c.UpdateAsync(TipoDocumentoSerie.Cobro, IdOtra, 7, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("caída"));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), Url, "config-numeracion", new Dictionary<string, string>
        {
            ["Input.Filas[0].Tipo"] = "2", ["Input.Filas[0].SerieId"] = IdOtra.ToString(), ["Input.Filas[0].Xmin"] = "4",
            ["Input.Filas[1].Tipo"] = "5", ["Input.Filas[1].SerieId"] = IdOtra.ToString(), ["Input.Filas[1].Xmin"] = "7",
            ["Input.Filas[2].Tipo"] = "6", ["Input.Filas[2].SerieId"] = IdOtra.ToString(), ["Input.Filas[2].Xmin"] = "8",
        });
        var html = HtmlSsr.Decodificar(await respuesta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("Guardados: Factura de venta. No fue posible guardar la configuración.", html);
        Assert.Contains("name=\"Input.Filas[0].Xmin\" value=\"9\"", html);
        // La fila que lanzó y la que no llegó a procesarse conservan la elección del administrador.
        Assert.Matches($"<select id=\"serie_2\"(?:(?!</select>).)*<option value=\"{IdOtra}\" selected", html);
        Assert.Matches($"<select id=\"serie_5\"(?:(?!</select>).)*<option value=\"{IdOtra}\" selected", html);
        Assert.Matches($"<select id=\"serie_6\"(?:(?!</select>).)*<option value=\"{IdOtra}\" selected", html);
        api.Verify(c => c.UpdateAsync(TipoDocumentoSerie.AsientoContable, It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Carga_SeriesDeLosTiposEnParalelo_YElFalloDeUnTipoSoloAfectaASuSelector()
    {
        using var app = new BlazorSsrFactory();
        var tipos = new[] { TipoDocumentoSerie.FacturaVenta, TipoDocumentoSerie.Cobro, TipoDocumentoSerie.AsientoContable };
        app.Simular<IConfiguracionNumeracionApiClient>()
            .Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. tipos.Select(t => Fila(t, IdSerie, $"S{(short)t}", 1))]);
        // Barrera: cada carga espera a que hayan empezado todas. En serie la primera agotaría el tiempo máximo y fallaría.
        var iniciadas = 0;
        var todas = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Simular<ISerieApiClient>()
            .Setup(c => c.ActivasDelTipoAsync(It.IsAny<TipoDocumentoSerie>(), It.IsAny<CancellationToken>()))
            .Returns(async (TipoDocumentoSerie tipo, CancellationToken ct) =>
            {
                if (Interlocked.Increment(ref iniciadas) == tipos.Length)
                {
                    todas.TrySetResult();
                }

                await todas.Task.WaitAsync(ct);
                if (tipo == TipoDocumentoSerie.Cobro)
                {
                    throw new HttpRequestException("caída");
                }

                return (IReadOnlyList<SerieResponse>)[Serie(IdSerie, "S", tipo), Serie(IdOtra, "OTRA", tipo)];
            });

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), Url);

        Assert.Matches($"<select id=\"serie_2\"(?:(?!</select>).)*<option value=\"{IdOtra}\"", html);
        Assert.Matches($"<select id=\"serie_6\"(?:(?!</select>).)*<option value=\"{IdOtra}\"", html);
        Assert.Matches("<select id=\"serie_5\"(?:(?!</select>).)*S5 \\(actual\\)", html);
        Assert.DoesNotMatch($"<select id=\"serie_5\"(?:(?!</select>).)*<option value=\"{IdOtra}\"", html);
        Assert.DoesNotContain("S2 (actual)", html);
        Assert.DoesNotContain("S6 (actual)", html);
    }

    private static ConfiguracionNumeracionResponse Fila(TipoDocumentoSerie tipo, Guid serieId, string codigo, long xmin) =>
        new() { TipoDocumento = tipo, SerieId = serieId, SerieCodigo = codigo, SerieActiva = true, Xmin = xmin };

    private static SerieResponse Serie(Guid id, string codigo, TipoDocumentoSerie tipo) =>
        new() { Id = id, Codigo = codigo, Descripcion = codigo, TipoDocumento = tipo, Activa = true, Xmin = 1 };

    private static void SimularSeries(BlazorSsrFactory app) =>
        app.Simular<ISerieApiClient>()
            .Setup(c => c.ActivasDelTipoAsync(It.IsAny<TipoDocumentoSerie>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TipoDocumentoSerie tipo, CancellationToken _) => [Serie(IdSerie, "FV", tipo), Serie(IdOtra, "FV2", tipo)]);
}
