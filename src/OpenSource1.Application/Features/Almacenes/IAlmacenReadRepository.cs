using OpenSource1.Application.Features.Almacenes.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Almacenes;

public interface IAlmacenReadRepository
{
    Task<AlmacenResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<AlmacenResponse>>> ListAsync(
        AlmacenSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default);
}
