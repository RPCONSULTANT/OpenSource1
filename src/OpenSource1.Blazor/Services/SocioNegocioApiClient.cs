using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

public sealed class SocioNegocioApiClient(HttpClient httpClient, ILogger<SocioNegocioApiClient> logger) : ISocioNegocioApiClient
{
    public async Task<PagedResult<SocioNegocioResponse>> ListAsync(SocioNegocioSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(BuildListUrl(filter, paginacion), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Clientes LIST returned {StatusCode}. Body: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"El servidor devolvió {(int)response.StatusCode} al obtener los clientes.",
                inner: null,
                statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<PagedResult<SocioNegocioResponse>>(cancellationToken)
            ?? PagedResult<SocioNegocioResponse>.Vacio(paginacion ?? new PageRequest());
    }

    public async Task<IReadOnlyList<SocioNegocioResponse>> ListAllAsync(SocioNegocioSearchFilter? filter = null, CancellationToken cancellationToken = default)
    {
        var items = new List<SocioNegocioResponse>();
        var pagina = 1;

        while (true)
        {
            var page = await ListAsync(filter, new PageRequest(pagina, PageRequest.TamanoMaximo), cancellationToken);
            items.AddRange(page.Items);

            if (page.Items.Count == 0 || items.Count >= page.Total)
            {
                break;
            }

            pagina++;
        }

        return items;
    }

    public async Task<SocioNegocioResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/socios-negocio/{id}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Clientes GET BY ID returned {StatusCode}. Body: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"El servidor devolvió {(int)response.StatusCode} al obtener el cliente.",
                inner: null,
                statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<SocioNegocioResponse>(cancellationToken);
    }

    private static string BuildListUrl(SocioNegocioSearchFilter? filter, PageRequest? paginacion = null)
    {
        var parameters = new List<string>();

        if (filter is null)
        {
            AddPaginationParameters(parameters, paginacion);
            return parameters.Count == 0 ? "api/socios-negocio" : $"api/socios-negocio?{string.Join("&", parameters)}";
        }

        if (!string.IsNullOrWhiteSpace(filter.NombreComercial))
        {
            parameters.Add($"nombreComercial={Uri.EscapeDataString(filter.NombreComercial.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Email))
        {
            parameters.Add($"email={Uri.EscapeDataString(filter.Email.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Telefono))
        {
            parameters.Add($"telefono={Uri.EscapeDataString(filter.Telefono.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Direccion))
        {
            parameters.Add($"direccion={Uri.EscapeDataString(filter.Direccion.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Sector))
        {
            parameters.Add($"sector={Uri.EscapeDataString(filter.Sector.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Pais))
        {
            parameters.Add($"pais={Uri.EscapeDataString(filter.Pais.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Codigo))
        {
            parameters.Add($"codigo={Uri.EscapeDataString(filter.Codigo.Trim())}");
        }

        if (filter.Tipo is { } tipo)
        {
            parameters.Add($"tipo={(short)tipo}");
        }

        if (!string.IsNullOrWhiteSpace(filter.NumeroDocumentoFiscal))
        {
            parameters.Add($"numeroDocumentoFiscal={Uri.EscapeDataString(filter.NumeroDocumentoFiscal.Trim())}");
        }

        AddPaginationParameters(parameters, paginacion);

        return parameters.Count == 0 ? "api/socios-negocio" : $"api/socios-negocio?{string.Join("&", parameters)}";
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

    public async Task<SocioNegocioOperationResult> CreateAsync(SocioNegocioInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/socios-negocio", input, cancellationToken);
        return await ToResultAsync(response, "Cliente registrado correctamente.", cancellationToken);
    }

    public async Task<SocioNegocioOperationResult> UpdateAsync(Guid id, SocioNegocioInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"api/socios-negocio/{id}", input, cancellationToken);
        return await ToResultAsync(response, "Cliente modificado correctamente.", cancellationToken);
    }

    public async Task<SocioNegocioOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"api/socios-negocio/{id}", cancellationToken);
        return await ToResultAsync(response, "Cliente eliminado correctamente.", cancellationToken);
    }

    private async Task<SocioNegocioOperationResult> ToResultAsync(
        HttpResponseMessage response, string successMessage, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            Guid? entityId = null;

            try
            {
                var payload = await response.Content.ReadFromJsonAsync<SocioNegocioResponse>(cancellationToken);
                entityId = payload?.Id;
            }
            catch
            {
                // ignore when response has no DTO body (delete/no-content)
            }

            return new SocioNegocioOperationResult(true, successMessage, entityId);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("Clientes API returned {StatusCode}. Body: {Body}", response.StatusCode, body);

        // Los 400/409/422 de la API traen mensajes de validación propios (texto seguro, en español):
        // se muestran tal cual. Si no se pueden leer, se cae al mensaje genérico de cada estado.
        var mensajes = response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity
            ? ExtraerMensajes(body)
            : [];

        var safe = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Debe iniciar sesión nuevamente.",
            HttpStatusCode.Forbidden => "No tiene permisos para realizar esta operación.",
            HttpStatusCode.NotFound => "No se encontró el cliente indicado.",
            HttpStatusCode.BadRequest => mensajes.Count > 0 ? "Revise los datos del formulario:" : "Revise los datos del formulario.",
            HttpStatusCode.Conflict => mensajes.Count > 0 ? string.Join(" ", mensajes) : "Ya existe un cliente con ese documento fiscal o la operación entra en conflicto con otro registro.",
            HttpStatusCode.UnprocessableEntity => mensajes.Count > 0 ? string.Join(" ", mensajes) : "La operación no está permitida para este cliente.",
            _ => "No fue posible completar la operación."
        };

        // En un 409/422 el mensaje ya es la lista completa; en un 400 la lista se muestra aparte, por campo.
        var errores = response.StatusCode == HttpStatusCode.BadRequest ? mensajes : [];
        return new SocioNegocioOperationResult(false, safe, Errors: errores);
    }

    /// <summary>
    /// Mensajes de un cuerpo de error de la API: el <c>ValidationProblemDetails</c> estándar
    /// (<c>errors</c> = objeto campo -> mensajes) o la lista plana de errores de binding
    /// (<c>errors</c> = array de textos). Sin duplicados; vacío si el cuerpo no tiene esa forma.
    /// </summary>
    private static List<string> ExtraerMensajes(string body)
    {
        var mensajes = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("errors", out var errores))
            {
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
