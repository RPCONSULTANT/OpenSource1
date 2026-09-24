using OpenSource1.Application.Features.CategoriasProducto.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.CategoriasProducto;

public interface ICategoriaProductoReadRepository
{
    Task<CategoriaProductoResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<CategoriaProductoResponse>>> ListAsync(
        CategoriaProductoSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default);
}
