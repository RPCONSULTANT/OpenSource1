using OpenSource1.Application.Features.FacturasVenta.Borradores.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.FacturasVenta.Borradores;

public interface IFacturaVentaBorradorReadRepository
{
    Task<FacturaVentaBorradorResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<FacturaVentaBorradorResponse>>> ListAsync(
        FacturaVentaBorradorSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default);
}
