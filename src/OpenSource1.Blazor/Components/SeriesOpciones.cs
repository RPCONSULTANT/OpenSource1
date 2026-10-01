using OpenSource1.Application.Features.Series.Dtos;
using OpenSource1.Blazor.Services;
using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Components;

/// <summary>Próximo número de una serie (vista previa, sin reserva): <c>Numero</c> y <c>Aviso</c>, o <c>Error</c> si no se puede numerar.</summary>
public sealed record ProximoVista(string? Numero, string? Aviso, string? Error);

/// <summary>
/// Carga de series para los selectores de los documentos y vista previa del próximo número (spec no-series), con el tiempo máximo
/// de <see cref="ProductoOpciones.TiempoMaximo"/>. Un fallo no rompe la página: series vacías con <c>CargaFallida</c> (que NO debe
/// bloquear Guardar la cabecera, F7: <see cref="Documento.SelectorSerie"/> conserva la serie actual) o un <see cref="ProximoVista"/> con Error.
/// </summary>
public static class SeriesOpciones
{
    public static async Task<OpcionesCargadas<SerieResponse>> ActivasAsync(
        ISerieApiClient cliente, TipoDocumentoSerie tipo, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ProductoOpciones.TiempoMaximo);
        try
        {
            return new OpcionesCargadas<SerieResponse>(await cliente.ActivasDelTipoAsync(tipo, cts.Token), false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load series of tipo {Tipo}.", tipo);
            return new OpcionesCargadas<SerieResponse>([], true);
        }
    }

    public static async Task<ProximoVista> ProximoAsync(
        ISerieApiClient cliente, Guid serieId, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ProductoOpciones.TiempoMaximo);
        try
        {
            var resultado = await cliente.ProximoAsync(serieId, cts.Token);
            if (resultado is { Succeeded: true, Valor: { } proximo })
            {
                return new ProximoVista(proximo.Numero, proximo.Aviso, null);
            }

            var detalle = resultado.Errors is { Count: > 0 } errores ? string.Join(" ", errores) : resultado.Message;
            return new ProximoVista(null, null, detalle);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load próximo número of serie {Id}.", serieId);
            return new ProximoVista(null, null, "No fue posible calcular el próximo número.");
        }
    }
}
