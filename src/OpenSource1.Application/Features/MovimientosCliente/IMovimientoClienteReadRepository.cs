using OpenSource1.Application.Features.MovimientosCliente.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.MovimientosCliente;

/// <summary>
/// Consultas del libro de clientes (Task 6.3). Importe restante, abierto y saldo se DERIVAN del detalle (D1, D7): nada de eso
/// está almacenado.
/// </summary>
public interface IMovimientoClienteReadRepository
{
    /// <summary>El socio existe y no está borrado lógicamente.</summary>
    Task<bool> ExisteSocioAsync(Guid socioNegocioId, CancellationToken cancellationToken = default);

    Task<PagedResult<MovimientoClienteResponse>> ListAsync(
        MovimientoClienteSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default);

    /// <summary>Movimientos del socio con restante ≠ 0, en orden cronológico (<c>FechaRegistro</c>, <c>Id</c>), sin paginar.</summary>
    Task<IReadOnlyList<MovimientoClienteResponse>> ListAbiertosAsync(Guid socioNegocioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Antigüedad de saldos por cliente a <see cref="EstadoCuentaCriterios.FechaCorte"/> (obligatoria aquí: el handler pone el
    /// valor por defecto), paginada por cliente, con los tramos sumados de todos los clientes filtrados.
    /// </summary>
    Task<EstadoCuentaResponse> GetEstadoCuentaAsync(
        EstadoCuentaCriterios criterios, PageRequest paginacion, CancellationToken cancellationToken = default);

    /// <summary><c>Σ detalle.Importe</c> de todos los movimientos del socio y cuántos siguen abiertos.</summary>
    Task<SaldoClienteResponse> GetSaldoAsync(Guid socioNegocioId, CancellationToken cancellationToken = default);
}
