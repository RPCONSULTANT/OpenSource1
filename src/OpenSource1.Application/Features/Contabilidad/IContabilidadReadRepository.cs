using OpenSource1.Application.Features.Contabilidad.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Contabilidad;

/// <summary>Consultas del libro contable: listados paginados (Task 5.5, filtros ampliados en la 7.4) y balance de comprobación (7.4).</summary>
public interface IContabilidadReadRepository
{
    Task<Result<PagedResult<MovimientoContableResponse>>> ListMovimientosAsync(
        MovimientoContableSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<RegistroContableResponse>>> ListRegistrosAsync(
        PageRequest paginacion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Balance de comprobación del rango: cuentas de Posteo con movimientos en el rango o saldo inicial distinto de cero y
    /// cuentas de Encabezado como títulos, ordenadas por número; totales de las de Posteo.
    /// </summary>
    Task<BalanceComprobacionResponse> GetBalanceComprobacionAsync(
        BalanceComprobacionCriterios criterios, CancellationToken cancellationToken = default);
}
