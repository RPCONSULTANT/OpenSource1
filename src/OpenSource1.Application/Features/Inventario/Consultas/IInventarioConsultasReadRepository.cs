using OpenSource1.Application.Features.Inventario.Consultas.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Inventario.Consultas;

/// <summary>
/// Vistas de solo lectura del libro de inventario (Task 7.2), paginadas y con orden por allow-list. Todo se DERIVA de
/// <c>MovimientosProducto</c>/<c>MovimientosValor</c> (D1): nada se almacena. Los criterios llegan ya validados por los handlers.
/// </summary>
public interface IInventarioConsultasReadRepository
{
    Task<PagedResult<MovimientoProductoVistaResponse>> ListMovimientosProductoAsync(
        MovimientoProductoVistaCriterios criterios, PageRequest paginacion, CancellationToken cancellationToken = default);

    Task<PagedResult<MovimientoValorVistaResponse>> ListMovimientosValorAsync(
        MovimientoValorVistaCriterios criterios, PageRequest paginacion, CancellationToken cancellationToken = default);

    /// <summary><paramref name="criterios"/> debe traer la fecha de corte ya resuelta.</summary>
    Task<ExistenciasVistaResponse> ListExistenciasAsync(
        ExistenciaVistaCriterios criterios, PageRequest paginacion, CancellationToken cancellationToken = default);
}
