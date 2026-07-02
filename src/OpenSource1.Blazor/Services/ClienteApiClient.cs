using System.Net;
using System.Net.Http.Json;
using OpenSource1.Application.Features.Clientes.Dtos;

namespace OpenSource1.Blazor.Services;

public sealed class ClienteApiClient(HttpClient httpClient, ILogger<ClienteApiClient> logger) : IClienteApiClient
{
    public async Task<IReadOnlyList<ClienteResponse>> ListAsync(ClienteSearchFilter? filter = null, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(BuildListUrl(filter), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Clientes LIST returned {StatusCode}. Body: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"El servidor devolvió {(int)response.StatusCode} al obtener los clientes.",
                inner: null,
                statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<ClienteResponse>>(cancellationToken) ?? [];
    }

    public async Task<ClienteResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/clientes/{id}", cancellationToken);

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

        return await response.Content.ReadFromJsonAsync<ClienteResponse>(cancellationToken);
    }

    private static string BuildListUrl(ClienteSearchFilter? filter)
    {
        if (filter is null)
        {
            return "api/clientes";
        }

        var parameters = new List<string>();

        if (filter.Id.HasValue)
        {
            parameters.Add($"id={Uri.EscapeDataString(filter.Id.Value.ToString())}");
        }

        if (!string.IsNullOrWhiteSpace(filter.DocumentoIdentidad))
        {
            parameters.Add($"documentoIdentidad={Uri.EscapeDataString(filter.DocumentoIdentidad.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Nombre))
        {
            parameters.Add($"nombre={Uri.EscapeDataString(filter.Nombre.Trim())}");
        }

        if (filter.Activo.HasValue)
        {
            parameters.Add($"activo={filter.Activo.Value.ToString().ToLowerInvariant()}");
        }

        return parameters.Count == 0 ? "api/clientes" : $"api/clientes?{string.Join("&", parameters)}";
    }

    public async Task<ClienteOperationResult> CreateAsync(ClienteInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/clientes", input, cancellationToken);
        return await ToResultAsync(response, "Cliente registrado correctamente.", cancellationToken);
    }

    public async Task<ClienteOperationResult> UpdateAsync(Guid id, ClienteInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"api/clientes/{id}", input, cancellationToken);
        return await ToResultAsync(response, "Cliente modificado correctamente.", cancellationToken);
    }

    public async Task<ClienteOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"api/clientes/{id}", cancellationToken);
        return await ToResultAsync(response, "Cliente eliminado correctamente.", cancellationToken);
    }

    private async Task<ClienteOperationResult> ToResultAsync(
        HttpResponseMessage response, string successMessage, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            Guid? entityId = null;

            try
            {
                var payload = await response.Content.ReadFromJsonAsync<ClienteResponse>(cancellationToken);
                entityId = payload?.Id;
            }
            catch
            {
                // ignore when response has no DTO body (delete/no-content)
            }

            return new ClienteOperationResult(true, successMessage, entityId);
        }

        var safe = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Debe iniciar sesión nuevamente.",
            HttpStatusCode.Forbidden => "No tiene permisos para realizar esta operación.",
            HttpStatusCode.NotFound => "No se encontró el cliente indicado.",
            HttpStatusCode.BadRequest => "Revise los datos del formulario.",
            _ => "No fue posible completar la operación."
        };

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("Clientes API returned {StatusCode}. Body: {Body}", response.StatusCode, body);
        return new ClienteOperationResult(false, safe);
    }
}
