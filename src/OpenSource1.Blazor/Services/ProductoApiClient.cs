using System.Net;
using System.Net.Http.Json;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

public sealed class ProductoApiClient(HttpClient httpClient, ILogger<ProductoApiClient> logger) : IProductoApiClient
{
    public async Task<PagedResult<ProductoResponse>> ListAsync(ProductoSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(BuildListUrl(filter, paginacion), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Productos LIST returned {StatusCode}. Body: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"El servidor devolvió {(int)response.StatusCode} al obtener los productos.",
                inner: null,
                statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<PagedResult<ProductoResponse>>(cancellationToken)
            ?? PagedResult<ProductoResponse>.Vacio(paginacion ?? new PageRequest());
    }

    public async Task<IReadOnlyList<ProductoResponse>> ListAllAsync(ProductoSearchFilter? filter = null, CancellationToken cancellationToken = default)
    {
        var items = new List<ProductoResponse>();
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

    public async Task<ProductoResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/productos/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Productos GET BY ID returned {StatusCode}. Body: {Body}", response.StatusCode, body);
            throw new HttpRequestException($"El servidor devolvió {(int)response.StatusCode} al obtener el producto.", inner: null, statusCode: response.StatusCode);
        }
        return await response.Content.ReadFromJsonAsync<ProductoResponse>(cancellationToken);
    }

    private static string BuildListUrl(ProductoSearchFilter? filter, PageRequest? paginacion = null)
    {
        var parameters = new List<string>();

        if (filter is null)
        {
            AddPaginationParameters(parameters, paginacion);
            return parameters.Count == 0 ? "api/productos" : $"api/productos?{string.Join("&", parameters)}";
        }

        if (!string.IsNullOrWhiteSpace(filter.Codigo))
        {
            parameters.Add($"codigo={Uri.EscapeDataString(filter.Codigo.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Nombre))
        {
            parameters.Add($"nombre={Uri.EscapeDataString(filter.Nombre.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(filter.CategoriaCodigo))
        {
            parameters.Add($"categoriaCodigo={Uri.EscapeDataString(filter.CategoriaCodigo.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(filter.CategoriaNombre))
        {
            parameters.Add($"categoriaNombre={Uri.EscapeDataString(filter.CategoriaNombre.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(filter.UnidadMedidaCodigo))
        {
            parameters.Add($"unidadMedidaCodigo={Uri.EscapeDataString(filter.UnidadMedidaCodigo.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(filter.UnidadMedidaNombre))
        {
            parameters.Add($"unidadMedidaNombre={Uri.EscapeDataString(filter.UnidadMedidaNombre.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Precio))
        {
            parameters.Add($"precio={Uri.EscapeDataString(filter.Precio.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Stock))
        {
            parameters.Add($"stock={Uri.EscapeDataString(filter.Stock.Trim())}");
        }

        AddPaginationParameters(parameters, paginacion);

        return parameters.Count == 0 ? "api/productos" : $"api/productos?{string.Join("&", parameters)}";
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

    public async Task<ProductoOperationResult> CreateAsync(ProductoInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/productos", input, cancellationToken);
        return await ToResultAsync(response, "Producto registrado correctamente.", cancellationToken);
    }

    public async Task<ProductoOperationResult> UpdateAsync(Guid id, ProductoInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"api/productos/{id}", input, cancellationToken);
        return await ToResultAsync(response, "Producto modificado correctamente.", cancellationToken);
    }

    public async Task<ProductoOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"api/productos/{id}", cancellationToken);
        return await ToResultAsync(response, "Producto eliminado correctamente.", cancellationToken);
    }

    private async Task<ProductoOperationResult> ToResultAsync(
        HttpResponseMessage response, string successMessage, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            Guid? entityId = null;
            try
            {
                var payload = await response.Content.ReadFromJsonAsync<ProductoResponse>(cancellationToken);
                entityId = payload?.Id;
            }
            catch { }
            return new ProductoOperationResult(true, successMessage, entityId);
        }

        var safe = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Debe iniciar sesión nuevamente.",
            HttpStatusCode.Forbidden => "No tiene permisos para realizar esta operación.",
            HttpStatusCode.NotFound => "No se encontró el producto indicado.",
            HttpStatusCode.BadRequest => "Revise los datos del formulario.",
            _ => "No fue posible completar la operación."
        };

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("Productos API returned {StatusCode}. Body: {Body}", response.StatusCode, body);
        return new ProductoOperationResult(false, safe);
    }
}
