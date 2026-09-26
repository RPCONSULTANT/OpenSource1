using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OpenSource1.Blazor.Services;

/// <summary>
/// Traducción de una respuesta de la API a <see cref="GrupoOperationResult"/> (clientes de grupos contables, Task 5.3): mismo
/// criterio que <c>CuentaContableApiClient</c> — los 400/409 traen mensajes propios (texto seguro, en español) que se muestran
/// tal cual; si no se pueden leer, se cae a un mensaje genérico por estado.
/// </summary>
internal static class ApiRespuestas
{
    public static async Task<GrupoOperationResult> ToResultAsync(
        HttpResponseMessage response, string successMessage, string entidad, ILogger logger, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            Guid? entityId = null;
            try
            {
                using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
                if (doc is not null && doc.RootElement.TryGetProperty("id", out var id) && id.TryGetGuid(out var guid))
                {
                    entityId = guid;
                }
            }
            catch
            {
                // Sin cuerpo (204 del borrado).
            }

            return new GrupoOperationResult(true, successMessage, entityId);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("API de {Entidad} devolvió {StatusCode}. Body: {Body}", entidad, response.StatusCode, body);

        var mensajes = response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict ? ExtraerMensajes(body) : [];

        var safe = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Debe iniciar sesión nuevamente.",
            HttpStatusCode.Forbidden => "No tiene permisos para realizar esta operación.",
            HttpStatusCode.NotFound => $"No se encontró el {entidad} indicado.",
            HttpStatusCode.BadRequest => mensajes.Count > 0 ? "Revise los datos del formulario:" : "Revise los datos del formulario.",
            HttpStatusCode.Conflict => mensajes.Count > 0
                ? string.Join(" ", mensajes)
                : $"La operación entra en conflicto con otro registro (código duplicado, {entidad} en uso o modificado por otro usuario).",
            _ => "No fue posible completar la operación."
        };

        return new GrupoOperationResult(false, safe, Errors: response.StatusCode == HttpStatusCode.BadRequest ? mensajes : []);
    }

    /// <summary>Mensajes de un <c>ValidationProblemDetails</c> (<c>errors</c> objeto o array) o, en su defecto, su <c>title</c>.</summary>
    public static List<string> ExtraerMensajes(string body)
    {
        var mensajes = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("errors", out var errores))
            {
                if (doc.RootElement.TryGetProperty("title", out var titulo) && titulo.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(titulo.GetString()))
                {
                    mensajes.Add(titulo.GetString()!);
                }

                return mensajes;
            }

            var valores = new List<JsonElement>();
            if (errores.ValueKind == JsonValueKind.Object)
            {
                foreach (var campo in errores.EnumerateObject())
                {
                    if (campo.Value.ValueKind == JsonValueKind.Array)
                    {
                        valores.AddRange(campo.Value.EnumerateArray());
                    }
                    else
                    {
                        valores.Add(campo.Value);
                    }
                }
            }
            else if (errores.ValueKind == JsonValueKind.Array)
            {
                valores.AddRange(errores.EnumerateArray());
            }

            foreach (var valor in valores)
            {
                var texto = valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;
                if (!string.IsNullOrWhiteSpace(texto) && !mensajes.Contains(texto))
                {
                    mensajes.Add(texto);
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            mensajes.Clear();
        }

        return mensajes;
    }

    public static string ConPaginacion(string ruta, List<string> parameters, OpenSource1.Core.Common.PageRequest? paginacion)
    {
        if (paginacion is not null)
        {
            parameters.Add($"pagina={paginacion.Pagina}");
            parameters.Add($"tamanoPagina={paginacion.TamanoPagina}");
            if (!string.IsNullOrWhiteSpace(paginacion.OrdenarPor))
            {
                parameters.Add($"ordenarPor={Uri.EscapeDataString(paginacion.OrdenarPor)}");
            }

            parameters.Add($"descendente={(paginacion.Descendente ? "true" : "false")}");
        }

        return parameters.Count == 0 ? ruta : $"{ruta}?{string.Join("&", parameters)}";
    }

    public static List<string> Filtros(GrupoContableSearchFilter? filter)
    {
        var parameters = new List<string>();
        if (!string.IsNullOrWhiteSpace(filter?.Codigo))
        {
            parameters.Add($"codigo={Uri.EscapeDataString(filter.Codigo.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(filter?.Descripcion))
        {
            parameters.Add($"descripcion={Uri.EscapeDataString(filter.Descripcion.Trim())}");
        }

        return parameters;
    }
}
