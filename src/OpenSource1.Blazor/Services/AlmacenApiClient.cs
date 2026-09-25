using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OpenSource1.Application.Features.Almacenes.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

public sealed class AlmacenApiClient(HttpClient httpClient, ILogger<AlmacenApiClient> logger) : IAlmacenApiClient
{
    public async Task<PagedResult<AlmacenResponse>> ListAsync(AlmacenSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(BuildListUrl(filter, paginacion), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Almacenes LIST returned {StatusCode}. Body: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"El servidor devolvió {(int)response.StatusCode} al obtener los almacenes.",
                inner: null,
                statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<PagedResult<AlmacenResponse>>(cancellationToken)
            ?? PagedResult<AlmacenResponse>.Vacio(paginacion ?? new PageRequest());
    }

    public async Task<AlmacenResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/almacenes/{id}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Almacenes GET BY ID returned {StatusCode}. Body: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"El servidor devolvió {(int)response.StatusCode} al obtener el almacén.",
                inner: null,
                statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<AlmacenResponse>(cancellationToken);
    }

    private static string BuildListUrl(AlmacenSearchFilter? filter, PageRequest? paginacion)
    {
        var parameters = new List<string>();

        if (filter is not null)
        {
            if (!string.IsNullOrWhiteSpace(filter.Codigo))
            {
                parameters.Add($"codigo={Uri.EscapeDataString(filter.Codigo.Trim())}");
            }

            if (!string.IsNullOrWhiteSpace(filter.Nombre))
            {
                parameters.Add($"nombre={Uri.EscapeDataString(filter.Nombre.Trim())}");
            }

            if (filter.Bloqueado is { } bloqueado)
            {
                parameters.Add($"bloqueado={(bloqueado ? "true" : "false")}");
            }
        }

        AddPaginationParameters(parameters, paginacion);

        return parameters.Count == 0 ? "api/almacenes" : $"api/almacenes?{string.Join("&", parameters)}";
    }

    private static void AddPaginationParameters(List<string> parameters, PageRequest? paginacion)
    {
        if (paginacion is null)
        {
            return;
        }

        parameters.Add($"pagina={paginacion.Pagina}");
        parameters.Add($"tamanoPagina={paginacion.TamanoPagina}");

        if (!string.IsNullOrWhiteSpace(paginacion.OrdenarPor))
        {
            parameters.Add($"ordenarPor={Uri.EscapeDataString(paginacion.OrdenarPor)}");
        }

        parameters.Add($"descendente={(paginacion.Descendente ? "true" : "false")}");
    }

    public async Task<AlmacenOperationResult> CreateAsync(AlmacenInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/almacenes", input, cancellationToken);
        return await ToResultAsync(response, "Almacén agregado correctamente.", cancellationToken);
    }

    public async Task<AlmacenOperationResult> UpdateAsync(Guid id, AlmacenInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"api/almacenes/{id}", input, cancellationToken);
        return await ToResultAsync(response, "Almacén modificado correctamente.", cancellationToken);
    }

    public async Task<AlmacenOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"api/almacenes/{id}", cancellationToken);
        return await ToResultAsync(response, "Almacén eliminado correctamente.", cancellationToken);
    }

    private async Task<AlmacenOperationResult> ToResultAsync(
        HttpResponseMessage response, string successMessage, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            Guid? entityId = null;

            try
            {
                var payload = await response.Content.ReadFromJsonAsync<AlmacenResponse>(cancellationToken);
                entityId = payload?.Id;
            }
            catch
            {
                // ignore when response has no DTO body (delete/no-content)
            }

            return new AlmacenOperationResult(true, successMessage, entityId);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("Almacenes API returned {StatusCode}. Body: {Body}", response.StatusCode, body);

        // Los 400/409 de la API traen mensajes de validación propios (texto seguro, en español): se
        // muestran tal cual. Si no se pueden leer, se cae al mensaje genérico de cada estado.
        var mensajes = response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict
            ? ExtraerMensajes(body)
            : [];

        var safe = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Debe iniciar sesión nuevamente.",
            HttpStatusCode.Forbidden => "No tiene permisos para realizar esta operación.",
            HttpStatusCode.NotFound => "No se encontró el almacén indicado.",
            HttpStatusCode.BadRequest => mensajes.Count > 0 ? "Revise los datos del formulario:" : "Revise los datos del formulario.",
            // El 409 puede venir de un Result.Fallo del handler (almacen.conflicto/predeterminado_requerido,
            // ValidationProblemDetails con "errors") o de una violación de índice único capturada por el
            // GlobalExceptionHandler (ProblemDetails simple, sin "errors", solo "title"): ExtraerMensajes
            // cubre ambos. Sin ninguno de los dos, el texto no debe asumir de cuál operación viene (alta,
            // modificación o baja usan el mismo 409).
            HttpStatusCode.Conflict => mensajes.Count > 0 ? string.Join(" ", mensajes) : "La operación entra en conflicto con otro registro (código duplicado, almacén predeterminado o movimientos existentes).",
            _ => "No fue posible completar la operación."
        };

        // En un 409 el mensaje ya es la lista completa; en un 400 la lista se muestra aparte, por campo.
        var errores = response.StatusCode == HttpStatusCode.BadRequest ? mensajes : [];
        return new AlmacenOperationResult(false, safe, Errors: errores);
    }

    /// <summary>
    /// Mensajes de un cuerpo de error de la API: el <c>ValidationProblemDetails</c> estándar
    /// (<c>errors</c> = objeto campo -> mensajes) o la lista plana de errores de binding
    /// (<c>errors</c> = array de textos), tal como los produce <c>ResultExtensions.ToActionResult</c>
    /// para los fallos de <c>Result</c> (código duplicado no es uno de estos: ver más abajo). Sin
    /// duplicados; vacío si el cuerpo no tiene esa forma.
    /// </summary>
    private static List<string> ExtraerMensajes(string body)
    {
        var mensajes = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("errors", out var errores))
            {
                // No es un ValidationProblemDetails: puede ser el ProblemDetails simple que arma
                // GlobalExceptionHandler para una violación de índice único (23505) capturada fuera
                // del Result del handler — no tiene "errors", solo "title" con el mensaje seguro.
                if (doc.RootElement.TryGetProperty("title", out var titulo) && titulo.ValueKind == JsonValueKind.String)
                {
                    var texto = titulo.GetString();
                    if (!string.IsNullOrWhiteSpace(texto))
                    {
                        mensajes.Add(texto);
                    }
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
}
