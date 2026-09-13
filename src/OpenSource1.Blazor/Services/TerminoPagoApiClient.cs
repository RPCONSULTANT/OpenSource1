using System.Net;
using System.Net.Http.Json;
using OpenSource1.Application.Features.TerminosPago.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

public sealed class TerminoPagoApiClient(HttpClient httpClient, ILogger<TerminoPagoApiClient> logger) : ITerminoPagoApiClient
{
    public async Task<PagedResult<TerminoPagoResponse>> ListAsync(TerminoPagoSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(BuildListUrl(filter, paginacion), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("TerminosPago LIST returned {StatusCode}. Body: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"El servidor devolvió {(int)response.StatusCode} al obtener los términos de pago.",
                inner: null,
                statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<PagedResult<TerminoPagoResponse>>(cancellationToken)
            ?? PagedResult<TerminoPagoResponse>.Vacio(paginacion ?? new PageRequest());
    }

    public async Task<TerminoPagoResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/terminos-pago/{id}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("TerminosPago GET BY ID returned {StatusCode}. Body: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"El servidor devolvió {(int)response.StatusCode} al obtener el término de pago.",
                inner: null,
                statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<TerminoPagoResponse>(cancellationToken);
    }

    private static string BuildListUrl(TerminoPagoSearchFilter? filter, PageRequest? paginacion)
    {
        var parameters = new List<string>();

        if (filter is not null)
        {
            if (!string.IsNullOrWhiteSpace(filter.Codigo))
            {
                parameters.Add($"codigo={Uri.EscapeDataString(filter.Codigo.Trim())}");
            }

            if (!string.IsNullOrWhiteSpace(filter.Descripcion))
            {
                parameters.Add($"descripcion={Uri.EscapeDataString(filter.Descripcion.Trim())}");
            }
        }

        AddPaginationParameters(parameters, paginacion);

        return parameters.Count == 0 ? "api/terminos-pago" : $"api/terminos-pago?{string.Join("&", parameters)}";
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

    public async Task<TerminoPagoOperationResult> CreateAsync(TerminoPagoInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/terminos-pago", input, cancellationToken);
        return await ToResultAsync(response, "Término de pago agregado correctamente.", cancellationToken);
    }

    public async Task<TerminoPagoOperationResult> UpdateAsync(Guid id, TerminoPagoInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"api/terminos-pago/{id}", input, cancellationToken);
        return await ToResultAsync(response, "Término de pago modificado correctamente.", cancellationToken);
    }

    public async Task<TerminoPagoOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"api/terminos-pago/{id}", cancellationToken);
        return await ToResultAsync(response, "Término de pago eliminado correctamente.", cancellationToken);
    }

    private async Task<TerminoPagoOperationResult> ToResultAsync(
        HttpResponseMessage response, string successMessage, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            Guid? entityId = null;

            try
            {
                var payload = await response.Content.ReadFromJsonAsync<TerminoPagoResponse>(cancellationToken);
                entityId = payload?.Id;
            }
            catch
            {
                // ignore when response has no DTO body (delete/no-content)
            }

            return new TerminoPagoOperationResult(true, successMessage, entityId);
        }

        var safe = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Debe iniciar sesión nuevamente.",
            HttpStatusCode.Forbidden => "No tiene permisos para realizar esta operación.",
            HttpStatusCode.NotFound => "No se encontró el término de pago indicado.",
            HttpStatusCode.Conflict => "Ya existe un término de pago con ese código.",
            HttpStatusCode.BadRequest => "Revise los datos del formulario.",
            _ => "No fue posible completar la operación."
        };

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("TerminosPago API returned {StatusCode}. Body: {Body}", response.StatusCode, body);
        return new TerminoPagoOperationResult(false, safe);
    }
}
