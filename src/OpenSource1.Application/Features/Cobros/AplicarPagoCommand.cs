using MediatR;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Cobros;

/// <summary>
/// Aplicación de un pago a una factura (spec 6.4 y 6.6, Task 6.5): inserta dos filas de detalle Aplicación (−importe en la factura,
/// +importe en el pago) que se apuntan mutuamente, SIN asiento (el importe ya está en el libro contable desde la factura y el
/// pago). Solo entre movimientos del MISMO socio, la "factura" con restante &gt; 0 y el "pago" con restante &lt; 0 (así también
/// sirve una nota de crédito o un ajuste con esos signos), por un importe ≤ el mínimo de los dos restantes. Es un
/// <see cref="IRequest{TResponse}"/> con transacción propia, como <see cref="RegistrarPagoClienteCommand"/>.
/// </summary>
/// <param name="Importe">&gt; 0, 2 decimales como máximo.</param>
/// <param name="FechaRegistro">Fecha de la aplicación en el detalle; por defecto, la fecha actual (UTC).</param>
public sealed record AplicarPagoCommand(
    long MovimientoFacturaId,
    long MovimientoPagoId,
    decimal Importe,
    DateOnly? FechaRegistro = null) : IRequest<Result<ResultadoAplicacionPago>>;

/// <summary>Aplicación hecha y los restantes de los dos movimientos después de ella.</summary>
public sealed record ResultadoAplicacionPago(
    long MovimientoFacturaId, long MovimientoPagoId, decimal Importe, decimal RestanteFactura, decimal RestantePago);
