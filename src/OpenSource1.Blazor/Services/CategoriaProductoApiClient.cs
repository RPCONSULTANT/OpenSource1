using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OpenSource1.Application.Features.CategoriasProducto.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

public sealed class CategoriaProductoApiClient(HttpClient httpClient, ILogger<CategoriaProductoApiClient> logger) : ICategoriaProductoApiClient
{
    private const string Ruta = "api/categorias-producto";

    public async Task<PagedResult<CategoriaProductoResponse>> ListAsync(CategoriaProductoSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(BuildListUrl(filter, paginacion), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("CategoriasProducto LIST returned {StatusCode}. Body: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"El servidor devolvió {(int)response.StatusCode} al obtener las categorías.",
                inner: null,
                statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<PagedResult<CategoriaProductoResponse>>(cancellationToken)
            ?? PagedResult<CategoriaProductoResponse>.Vacio(paginacion ?? new PageRequest());
    }

    public async Task<IReadOnlyList<CategoriaProductoResponse>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var todas = new List<CategoriaProductoResponse>();
        var pagina = 1;

        while (true)
        {
            var resultado = await ListAsync(
                filter: null,
                new PageRequest(pagina, PageRequest.TamanoMaximo, "Nombre", Descendente: false),
                cancellationToken);

            todas.AddRange(resultado.Items);

            if (pagina >= resultado.TotalPaginas)
            {
                return todas;
            }

            pagina++;
        }
    }

    public async Task<CategoriaProductoResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"{Ruta}/{id}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("CategoriasProducto GET BY ID returned {StatusCode}. Body: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                $"El servidor devolvió {(int)response.StatusCode} al obtener la categoría.",
                inner: null,
                statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<CategoriaProductoResponse>(cancellationToken);
    }

    private static string BuildListUrl(CategoriaProductoSearchFilter? filter, PageRequest? paginacion)
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

        return parameters.Count == 0 ? Ruta : $"{Ruta}?{string.Join("&", parameters)}";
    }

    public async Task<CategoriaProductoOperationResult> CreateAsync(CategoriaProductoInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(Ruta, input, cancellationToken);
        return await ToResultAsync(
            response, "Categoría agregada correctamente.", "Ya existe una categoría con ese código.", cancellationToken);
    }

    public async Task<CategoriaProductoOperationResult> UpdateAsync(Guid id, CategoriaProductoInput input, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"{Ruta}/{id}", input, cancellationToken);
        return await ToResultAsync(
            response, "Categoría modificada correctamente.", "Ya existe una categoría con ese código.", cancellationToken);
    }

    public async Task<CategoriaProductoOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"{Ruta}/{id}", cancellationToken);
        return await ToResultAsync(
            response, "Categoría eliminada correctamente.",
            "No se puede eliminar la categoría porque tiene subcategorías asociadas.", cancellationToken);
    }

    private async Task<CategoriaProductoOperationResult> ToResultAsync(
        HttpResponseMessage response, string successMessage, string conflictMessage, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            Guid? entityId = null;

            try
            {
                var payload = await response.Content.ReadFromJsonAsync<CategoriaProductoResponse>(cancellationToken);
                entityId = payload?.Id;
            }
            catch
            {
                // ignore when response has no DTO body (delete/no-content)
            }

            return new CategoriaProductoOperationResult(true, successMessage, entityId);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("CategoriasProducto API returned {StatusCode}. Body: {Body}", response.StatusCode, body);

        var safe = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Debe iniciar sesión nuevamente.",
            HttpStatusCode.Forbidden => "No tiene permisos para realizar esta operación.",
            HttpStatusCode.NotFound => "No se encontró la categoría indicada.",
            HttpStatusCode.Conflict => conflictMessage,
            // Los mensajes de validación de la API son texto propio (p. ej. el de "crearía un ciclo"),
            // seguros para mostrar al usuario; si no se pueden leer se cae al mensaje genérico.
            HttpStatusCode.BadRequest => ExtraerMensajesDeValidacion(body) ?? "Revise los datos del formulario.",
            _ => "No fue posible completar la operación."
        };

        return new CategoriaProductoOperationResult(false, safe);
    }

    private static string? ExtraerMensajesDeValidacion(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("errors", out var errores) || errores.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var mensajes = errores.EnumerateObject()
                .SelectMany(campo => campo.Value.EnumerateArray())
                .Select(m => m.GetString())
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .ToList();

            return mensajes.Count == 0 ? null : string.Join(" ", mensajes);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}
