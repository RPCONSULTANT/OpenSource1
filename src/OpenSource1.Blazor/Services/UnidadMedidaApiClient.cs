using System.Net;
using System.Net.Http.Json;
using OpenSource1.Application.Features.UnidadesMedida.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

public sealed class UnidadMedidaApiClient(HttpClient httpClient, ILogger<UnidadMedidaApiClient> logger) : IUnidadMedidaApiClient
{
    public async Task<PagedResult<UnidadMedidaResponse>> ListAsync(UnidadMedidaSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(BuildListUrl(filter, paginacion), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("UnidadesMedida LIST returned {StatusCode}. Body: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"El servidor devolvió {(int)response.StatusCode} al obtener las unidades de medida.",
                inner: null,
                statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<PagedResult<UnidadMedidaResponse>>(cancellationToken)
            ?? PagedResult<UnidadMedidaResponse>.Vacio(paginacion ?? new PageRequest());
    }

    public async Task<UnidadMedidaResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/unidades-medida/{id}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("UnidadesMedida GET BY ID returned {StatusCode}. Body: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"El servidor devolvió {(int)response.StatusCode} al obtener la unidad de medida.",
                inner: null,
                statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<UnidadMedidaResponse>(cancellationToken);
    }

    private static string BuildListUrl(UnidadMedidaSearchFilter? filter, PageRequest? paginacion)
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
        }

        AddPaginationParameters(parameters, paginacion);

        return parameters.Count == 0 ? "api/unidades-medida" : $"api/unidades-medida?{string.Join("&", parameters)}";
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

    public async Task<UnidadMedidaOperationResult> CreateAsync(UnidadMedidaInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/unidades-medida", input, cancellationToken);
        return await ToResultAsync(response, "Unidad de medida agregada correctamente.", cancellationToken);
    }

    public async Task<UnidadMedidaOperationResult> UpdateAsync(Guid id, UnidadMedidaInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"api/unidades-medida/{id}", input, cancellationToken);
        return await ToResultAsync(response, "Unidad de medida modificada correctamente.", cancellationToken);
    }

    public async Task<UnidadMedidaOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"api/unidades-medida/{id}", cancellationToken);
        return await ToResultAsync(response, "Unidad de medida eliminada correctamente.", cancellationToken);
    }

    private async Task<UnidadMedidaOperationResult> ToResultAsync(
        HttpResponseMessage response, string successMessage, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            Guid? entityId = null;

            try
            {
                var payload = await response.Content.ReadFromJsonAsync<UnidadMedidaResponse>(cancellationToken);
                entityId = payload?.Id;
            }
            catch
            {
                // ignore when response has no DTO body (delete/no-content)
            }

            return new UnidadMedidaOperationResult(true, successMessage, entityId);
        }

        var safe = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Debe iniciar sesión nuevamente.",
            HttpStatusCode.Forbidden => "No tiene permisos para realizar esta operación.",
            HttpStatusCode.NotFound => "No se encontró la unidad de medida indicada.",
            HttpStatusCode.Conflict => "Ya existe una unidad de medida con ese código, o la unidad está asociada a productos.",
            HttpStatusCode.BadRequest => "Revise los datos del formulario.",
            _ => "No fue posible completar la operación."
        };

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("UnidadesMedida API returned {StatusCode}. Body: {Body}", response.StatusCode, body);
        return new UnidadMedidaOperationResult(false, safe);
    }
}
