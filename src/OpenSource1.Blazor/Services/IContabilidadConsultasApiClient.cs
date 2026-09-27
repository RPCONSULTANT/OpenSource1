using OpenSource1.Application.Features.Contabilidad;
using OpenSource1.Application.Features.Contabilidad.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Services;

/// <summary>
/// Cliente tipado de las vistas contables (Task 7.4): <c>GET api/contabilidad/movimientos</c> con todos sus filtros y
/// <c>GET api/contabilidad/balance-comprobacion</c>. Un 400 devuelve los mensajes reales de la API en
/// <see cref="ConsultaResultado{T}.Errors"/>.
/// </summary>
public interface IContabilidadConsultasApiClient
{
    Task<ConsultaResultado<PagedResult<MovimientoContableResponse>>> ListMovimientosAsync(
        MovimientoContableSearchCriteria criterios, PageRequest paginacion, CancellationToken cancellationToken = default);

    Task<ConsultaResultado<BalanceComprobacionResponse>> GetBalanceComprobacionAsync(
        BalanceComprobacionCriterios criterios, CancellationToken cancellationToken = default);
}
