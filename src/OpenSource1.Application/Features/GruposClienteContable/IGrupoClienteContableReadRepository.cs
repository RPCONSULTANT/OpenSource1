using OpenSource1.Application.Features.GruposClienteContable.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.GruposClienteContable;

public interface IGrupoClienteContableReadRepository
{
    Task<GrupoClienteContableResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<GrupoClienteContableResponse>>> ListAsync(
        GrupoClienteContableSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default);
}
