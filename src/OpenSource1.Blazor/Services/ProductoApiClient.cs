using System.Net;
using System.Net.Http.Json;
using OpenSource1.Application.Features.Productos.Dtos;

namespace OpenSource1.Blazor.Services;

public sealed class ProductoApiClient(HttpClient httpClient, ILogger<ProductoApiClient> logger) : IProductoApiClient
{
    public async Task<IReadOnlyList<ProductoResponse>> ListAsync(ProductoSearchFilter? filter = null, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(BuildListUrl(filter), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Productos LIST returned {StatusCode}. Body: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"El servidor devolvió {(int)response.StatusCode} al obtener los productos.",
                inner: null,
                statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<ProductoResponse>>(cancellationToken) ?? [];
    }

    private static string BuildListUrl(ProductoSearchFilter? filter)
    {
        if (filter is null)
        {
            return "api/productos";
        }

        var parameters = new List<string>();

        if (filter.Id.HasValue)
        {
            parameters.Add($"id={Uri.EscapeDataString(filter.Id.Value.ToString())}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Codigo))
        {
            parameters.Add($"codigo={Uri.EscapeDataString(filter.Codigo.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Nombre))
        {
            parameters.Add($"nombre={Uri.EscapeDataString(filter.Nombre.Trim())}");
        }

        if (filter.Activo.HasValue)
        {
            parameters.Add($"activo={filter.Activo.Value.ToString().ToLowerInvariant()}");
        }

        return parameters.Count == 0 ? "api/productos" : $"api/productos?{string.Join("&", parameters)}";
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
            return new ProductoOperationResult(true, successMessage);
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
