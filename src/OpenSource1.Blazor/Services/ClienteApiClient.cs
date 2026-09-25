using System.Net;
using System.Net.Http.Json;
using OpenSource1.Application.Features.Clientes.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

public sealed class ClienteApiClient(HttpClient httpClient, ILogger<ClienteApiClient> logger) : IClienteApiClient
{
    public async Task<PagedResult<ClienteResponse>> ListAsync(ClienteSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default)
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

        return await response.Content.ReadFromJsonAsync<PagedResult<ClienteResponse>>(cancellationToken)
            ?? PagedResult<ClienteResponse>.Vacio(paginacion ?? new PageRequest());
    }

    public async Task<IReadOnlyList<ClienteResponse>> ListAllAsync(ClienteSearchFilter? filter = null, CancellationToken cancellationToken = default)
    {
        var items = new List<ClienteResponse>();
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

    private static string BuildListUrl(ClienteSearchFilter? filter, PageRequest? paginacion = null)
    {
        var parameters = new List<string>();

        if (filter is null)
        {
            AddPaginationParameters(parameters, paginacion);
            return parameters.Count == 0 ? "api/clientes" : $"api/clientes?{string.Join("&", parameters)}";
        }

        if (!string.IsNullOrWhiteSpace(filter.Apellido))
        {
            parameters.Add($"apellido={Uri.EscapeDataString(filter.Apellido.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Nombre))
        {
            parameters.Add($"nombre={Uri.EscapeDataString(filter.Nombre.Trim())}");
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

        AddPaginationParameters(parameters, paginacion);

        return parameters.Count == 0 ? "api/clientes" : $"api/clientes?{string.Join("&", parameters)}";
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
