using OpenSource1.Application.Features.CategoriasProducto.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

public interface ICategoriaProductoApiClient
{
    Task<PagedResult<CategoriaProductoResponse>> ListAsync(CategoriaProductoSearchFilter? filter = null, PageRequest? paginacion = null, CancellationToken cancellationToken = default);

    /// <summary>Recorre todas las páginas del listado; alimenta el selector de categoría padre.</summary>
    Task<IReadOnlyList<CategoriaProductoResponse>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<CategoriaProductoResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CategoriaProductoOperationResult> CreateAsync(CategoriaProductoInput input, CancellationToken cancellationToken = default);
    Task<CategoriaProductoOperationResult> UpdateAsync(Guid id, CategoriaProductoInput input, CancellationToken cancellationToken = default);
    Task<CategoriaProductoOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record CategoriaProductoSearchFilter(string? Codigo, string? Nombre);

public sealed record CategoriaProductoInput(
    string Codigo, string Nombre, Guid? CategoriaPadreId);

public sealed record CategoriaProductoOperationResult(bool Succeeded, string Message, Guid? EntityId = null);
