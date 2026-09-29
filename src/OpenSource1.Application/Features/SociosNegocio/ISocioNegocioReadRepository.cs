using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.SociosNegocio;

public interface ISocioNegocioReadRepository
{
    Task<SocioNegocioResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<SocioNegocioResponse>>> ListAsync(
        SocioNegocioSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default);
}
