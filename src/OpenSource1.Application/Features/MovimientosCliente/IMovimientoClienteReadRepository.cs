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

    /// <summary><c>Σ detalle.Importe</c> de todos los movimientos del socio y cuántos siguen abiertos.</summary>
    Task<SaldoClienteResponse> GetSaldoAsync(Guid socioNegocioId, CancellationToken cancellationToken = default);
}
