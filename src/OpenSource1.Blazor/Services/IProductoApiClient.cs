using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

public interface IProductoApiClient
{
    Task<PagedResult<ProductoResponse>> ListAsync(ProductoSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera todos los productos que coincidan con el filtro, paginando internamente contra la
    /// API con el tamaño de página máximo. Uso reservado a consumidores que necesitan el conjunto
    /// completo (reportes crudos, paneles con agregados) y no a los listados paginados.
    /// </summary>
    Task<IReadOnlyList<ProductoResponse>> ListAllAsync(ProductoSearchFilter? filter = null, CancellationToken cancellationToken = default);

    Task<ProductoResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductoOperationResult> CreateAsync(ProductoInput input, CancellationToken cancellationToken = default);
    Task<ProductoOperationResult> UpdateAsync(Guid id, ProductoInput input, CancellationToken cancellationToken = default);
    Task<ProductoOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record ProductoSearchFilter(
    string? Codigo,
    string? Nombre,
    string? CategoriaCodigo,
    string? CategoriaNombre,
    string? UnidadMedidaCodigo,
    string? UnidadMedidaNombre,
    string? Precio,
    string? Stock);

public sealed record ProductoInput(
    string Codigo,
    string Nombre,
    decimal Precio,
    int Stock,
    string CategoriaCodigo,
    string CategoriaNombre,
    string UnidadMedidaCodigo,
    string? ImagePath = null);

public sealed record ProductoOperationResult(bool Succeeded, string Message, Guid? EntityId = null);
