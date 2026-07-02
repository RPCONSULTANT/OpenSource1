using OpenSource1.Application.Features.Productos.Dtos;

namespace OpenSource1.Blazor.Services;

public interface IProductoApiClient
{
    Task<IReadOnlyList<ProductoResponse>> ListAsync(ProductoSearchFilter? filter = null, CancellationToken cancellationToken = default);
    Task<ProductoOperationResult> CreateAsync(ProductoInput input, CancellationToken cancellationToken = default);
    Task<ProductoOperationResult> UpdateAsync(Guid id, ProductoInput input, CancellationToken cancellationToken = default);
    Task<ProductoOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record ProductoSearchFilter(
    Guid? Id,
    string? Codigo,
    string? Nombre,
    bool? Activo);

public sealed record ProductoInput(
    string Codigo,
    string Nombre,
    string? Descripcion,
    decimal Precio,
    int Stock,
    bool Activo);

public sealed record ProductoOperationResult(bool Succeeded, string Message);
