using OpenSource1.Application.Features.TerminosPago.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.TerminosPago;

public interface ITerminoPagoReadRepository
{
    Task<TerminoPagoResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<TerminoPagoResponse>>> ListAsync(
        TerminoPagoSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default);
}
