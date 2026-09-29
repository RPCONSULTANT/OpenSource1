using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OpenSource1.Blazor.Services;

/// <summary>
/// Traducción común de las respuestas de <c>api/facturas-venta</c>, <c>api/cobros</c> y <c>api/clientes</c> (Task 6.6). Mismo
/// criterio que <see cref="DiarioInventarioApiClient"/>: los 400/409/422 traen mensajes reales que se muestran tal cual (un 400
/// los separa en <c>Errors</c>: los de posteo ya llevan "Línea n:" en el texto); el resto usa mensajes genéricos por estado.
/// </summary>
internal static class VentaApiRespuestas
{
    public static async Task<VentaOperationResult<T>> ToResultAsync<T>(
        HttpResponseMessage response, string successMessage, string notFoundMessage, string entidad, ILogger logger,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            T? payload;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "API de {Entidad} devolvió {StatusCode} con un cuerpo ilegible.", entidad, response.StatusCode);
                payload = default;
            }

            return payload is null
                ? new VentaOperationResult<T>(false, $"La API no devolvió el resultado esperado ({entidad}).")
                : new VentaOperationResult<T>(true, successMessage, payload);
        }

        return await ErrorAsync<T>(response, notFoundMessage, entidad, logger, cancellationToken);
    }

    /// <summary>Para respuestas sin cuerpo relevante (204 del borrado).</summary>
    public static async Task<VentaOperationResult<bool>> ToPlainResultAsync(
        HttpResponseMessage response, string successMessage, string notFoundMessage, string entidad, ILogger logger,
        CancellationToken cancellationToken) =>
        response.IsSuccessStatusCode
            ? new VentaOperationResult<bool>(true, successMessage, true)
            : await ErrorAsync<bool>(response, notFoundMessage, entidad, logger, cancellationToken);

    /// <summary>GET de un recurso: 404 = <see langword="null"/>; cualquier otro error lanza <see cref="HttpRequestException"/>.</summary>
    public static async Task<T?> GetOrNullAsync<T>(
        HttpClient httpClient, string url, string entidad, ILogger logger, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        await AsegurarExitoAsync(response, entidad, logger, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    public static async Task AsegurarExitoAsync(HttpResponseMessage response, string entidad, ILogger logger, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("API de {Entidad} devolvió {StatusCode}. Body: {Body}", entidad, response.StatusCode, body);
        throw new HttpRequestException(
            $"El servidor devolvió {(int)response.StatusCode} al obtener {entidad}.", inner: null, statusCode: response.StatusCode);
    }

    private static async Task<VentaOperationResult<T>> ErrorAsync<T>(
        HttpResponseMessage response, string notFoundMessage, string entidad, ILogger logger, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("API de {Entidad} devolvió {StatusCode}. Body: {Body}", entidad, response.StatusCode, body);

        var mensajes = response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity
            ? ApiRespuestas.ExtraerMensajes(body)
            : [];

        var safe = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Debe iniciar sesión nuevamente.",
            HttpStatusCode.Forbidden => "No tiene permisos para realizar esta operación.",
            HttpStatusCode.NotFound => notFoundMessage,
            HttpStatusCode.BadRequest => mensajes.Count > 0 ? "Revise los datos:" : "Revise los datos del formulario.",
            HttpStatusCode.Conflict => mensajes.Count > 0
                ? string.Join(" ", mensajes)
                : "La operación entra en conflicto con otro registro (modificado o posteado por otro usuario).",
            HttpStatusCode.UnprocessableEntity => mensajes.Count > 0 ? string.Join(" ", mensajes) : "El registro está bloqueado.",
            _ => "No fue posible completar la operación."
        };

        return new VentaOperationResult<T>(false, safe, Errors: response.StatusCode == HttpStatusCode.BadRequest ? mensajes : []);
    }
}
