using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

        if (!string.IsNullOrWhiteSpace(filter.PrecioVenta))
        {
            parameters.Add($"precioVenta={Uri.EscapeDataString(filter.PrecioVenta.Trim())}");
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

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("Productos API returned {StatusCode}. Body: {Body}", response.StatusCode, body);

        // Los 400/409/422 de la API traen mensajes de validación propios (texto seguro, en español): se muestran tal cual. Si no se
        // pueden leer, se cae al mensaje genérico de cada estado.
        var mensajes = response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity
            ? ExtraerMensajes(body)
            : [];

        var safe = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Debe iniciar sesión nuevamente.",
            HttpStatusCode.Forbidden => "No tiene permisos para realizar esta operación.",
            HttpStatusCode.NotFound => "No se encontró el producto indicado.",
            HttpStatusCode.BadRequest => mensajes.Count > 0 ? "Revise los datos del formulario:" : "Revise los datos del formulario.",
            HttpStatusCode.Conflict => mensajes.Count > 0 ? string.Join(" ", mensajes) : "Ya existe un producto con ese código o la operación entra en conflicto con otro registro.",
            HttpStatusCode.UnprocessableEntity => mensajes.Count > 0 ? string.Join(" ", mensajes) : "La operación no está permitida para este producto.",
            _ => "No fue posible completar la operación."
        };

        // En un 409/422 el mensaje ya es la lista completa; en un 400 la lista se muestra aparte, por campo.
        var errores = response.StatusCode == HttpStatusCode.BadRequest ? mensajes : [];
        return new ProductoOperationResult(false, safe, Errors: errores);
    }

    /// <summary>
    /// Mensajes de un cuerpo de error de la API: el <c>ValidationProblemDetails</c> estándar (<c>errors</c> = objeto campo -> mensajes)
    /// o la lista plana de errores de binding (<c>errors</c> = array de textos). Sin duplicados; vacío si el cuerpo no tiene esa forma.
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
