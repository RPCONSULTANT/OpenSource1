using OpenSource1.Application.Features.CuentasContables.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.CuentasContables;

public interface ICuentaContableReadRepository
{
    Task<CuentaContableResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<CuentaContableResponse>>> ListAsync(
        CuentaContableSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default);
}
