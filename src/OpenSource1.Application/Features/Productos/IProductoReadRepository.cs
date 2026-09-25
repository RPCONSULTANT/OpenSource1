using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Productos;

public interface IProductoReadRepository
{
    Task<ProductoResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<ProductoResponse>>> ListAsync(
        ProductoSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default);
}
