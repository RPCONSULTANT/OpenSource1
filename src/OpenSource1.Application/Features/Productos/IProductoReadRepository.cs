using OpenSource1.Application.Features.Productos.Dtos;

namespace OpenSource1.Application.Features.Productos;

public interface IProductoReadRepository
{
    Task<ProductoResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductoResponse>> ListAsync(ProductoSearchCriteria search, CancellationToken cancellationToken = default);
}
