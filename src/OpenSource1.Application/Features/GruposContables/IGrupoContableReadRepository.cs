using OpenSource1.Application.Features.GruposContables.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.GruposContables;

public interface IGrupoContableReadRepository
{
    Task<GrupoContableResponse?> GetByIdAsync(TipoGrupoContable tipo, Guid id, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<GrupoContableResponse>>> ListAsync(
        TipoGrupoContable tipo, GrupoContableSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default);
}
