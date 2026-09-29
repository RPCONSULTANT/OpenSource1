namespace OpenSource1.Application.Features.MovimientosCliente.Dtos;

/// <summary>Saldo DERIVADO de un cliente: <c>Σ detalle.Importe</c> de todos sus movimientos (+ nos debe) y cuántos siguen abiertos.</summary>
public sealed record SaldoClienteResponse(Guid SocioNegocioId, decimal Saldo, int MovimientosAbiertos);
