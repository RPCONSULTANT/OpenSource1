using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OpenSource1.Blazor.Services;

/// <summary>
/// GET común de las vistas de consulta de la Fase 7 (inventario, clientes): 200 con cuerpo → valor; 200 sin cuerpo legible →
/// fallo explícito; 400 → "Revise los filtros:" con los mensajes REALES de la API; 401/403/404/otros → mensaje genérico. Los
/// fallos de red se propagan como excepción (la página los captura y responde igualmente con un aviso).
/// </summary>
internal static class ConsultasApi
{
    public static async Task<ConsultaResultado<T>> GetAsync<T>(
        HttpClient httpClient, string url, string entidad, string sinPermiso, ILogger logger,
        string? noEncontrado = null, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            T? payload;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "La API devolvió {StatusCode} con un cuerpo ilegible al obtener {Entidad}.", response.StatusCode, entidad);
                payload = default;
            }

            return payload is null
                ? new ConsultaResultado<T>(false, $"La API no devolvió {entidad}.")
                : new ConsultaResultado<T>(true, string.Empty, payload);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("La API devolvió {StatusCode} al obtener {Entidad}. Body: {Body}", response.StatusCode, entidad, body);

        var mensajes = response.StatusCode == HttpStatusCode.BadRequest ? ApiRespuestas.ExtraerMensajes(body) : [];
        var safe = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Debe iniciar sesión nuevamente.",
            HttpStatusCode.Forbidden => sinPermiso,
            HttpStatusCode.NotFound when noEncontrado is not null => noEncontrado,
            HttpStatusCode.BadRequest => mensajes.Count > 0 ? "Revise los filtros:" : "Revise los filtros.",
            _ => $"No fue posible cargar {entidad}."
        };
        return new ConsultaResultado<T>(false, safe, Errors: mensajes);
    }
}
