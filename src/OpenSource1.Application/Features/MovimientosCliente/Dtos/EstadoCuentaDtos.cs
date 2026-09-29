using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.MovimientosCliente.Dtos;

/// <summary>
/// Antigüedad de saldos (Task 7.3) sobre el restante de cada documento A LA FECHA DE CORTE. Los restantes positivos se reparten
/// por días vencidos (<c>fechaCorte − FechaVencimiento</c>): <see cref="Corriente"/> (no vencido, ≤ 0 días), 1-30, 31-60, 61-90 y
/// más de 90; los negativos (pagos o notas de crédito sin aplicar) van a <see cref="SinAplicar"/> y restan.
/// <see cref="Total"/> = suma de todos los tramos = saldo del cliente a la fecha de corte.
/// </summary>
public class EstadoCuentaTramos
{
    public decimal Corriente { get; init; }
    public decimal Dias1a30 { get; init; }
    public decimal Dias31a60 { get; init; }
    public decimal Dias61a90 { get; init; }
    public decimal Mas90 { get; init; }
    public decimal SinAplicar { get; init; }
    public decimal Total { get; init; }
}

/// <summary>Fila del estado de cuenta: un cliente con sus tramos y cuántos documentos tiene abiertos a la fecha de corte.</summary>
public sealed class EstadoCuentaClienteResponse : EstadoCuentaTramos
{
    public Guid SocioNegocioId { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public int DocumentosAbiertos { get; init; }
}

/// <summary>Página de clientes, la fecha de corte aplicada y los tramos sumados de TODOS los clientes filtrados (no solo de la página).</summary>
public sealed record EstadoCuentaResponse(PagedResult<EstadoCuentaClienteResponse> Pagina, DateOnly FechaCorte, EstadoCuentaTramos Totales);
