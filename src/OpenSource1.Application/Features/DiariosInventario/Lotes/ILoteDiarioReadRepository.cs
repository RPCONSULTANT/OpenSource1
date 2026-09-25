using OpenSource1.Application.Features.DiariosInventario.Lotes.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.DiariosInventario.Lotes;

public interface ILoteDiarioReadRepository
{
    Task<LoteDiarioResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<LoteDiarioResponse>>> ListAsync(
        LoteDiarioSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default);
}
