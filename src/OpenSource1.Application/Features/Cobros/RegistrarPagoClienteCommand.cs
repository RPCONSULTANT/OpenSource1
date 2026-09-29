using MediatR;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Cobros;

/// <summary>
/// Registro de un pago de cliente (spec 6.6 con las desviaciones de la Fase 6, Task 6.5): movimiento de cliente Pago
/// (<c>ImporteOriginal = −Importe</c>) con su detalle Pago, número de la serie <c>COBRO</c> (sin huecos) y asiento débito
/// caja/banco / crédito CxC. Es un <see cref="IRequest{TResponse}"/> y NO un <c>ICommand</c> (mismo criterio que
/// <c>PostearFacturaVentaCommand</c>): el handler abre y confirma UNA transacción propia y lanza
/// <see cref="InvalidOperationException"/> si ya hay una activa.
/// </summary>
/// <param name="SocioNegocioId">El cliente que paga. Debe existir; un socio bloqueado <c>Todo</c> no puede pagar (uno bloqueado solo
/// para <c>Facturacion</c> sí: el bloqueo de facturación impide venderle, no cobrarle lo que debe).</param>
/// <param name="Importe">&gt; 0, 2 decimales como máximo, menor que 1e14 (cota de <c>numeric(18,4)</c>).</param>
/// <param name="FechaRegistro">Fecha de registro del movimiento y del asiento (obligatoria).</param>
/// <param name="FechaDocumento">Fecha del documento; por defecto la de registro. También es la de vencimiento del movimiento.</param>
/// <param name="CuentaCajaId">Cuenta de caja/banco que se debita: de Posteo, no bloqueada y de posteo directo. Por defecto
/// <c>1101 Caja</c>.</param>
/// <param name="Descripcion">Opcional (200 como máximo); por defecto "Cobro {número}".</param>
public sealed record RegistrarPagoClienteCommand(
    Guid SocioNegocioId,
    decimal Importe,
    DateOnly FechaRegistro,
    DateOnly? FechaDocumento = null,
    Guid? CuentaCajaId = null,
    string? Descripcion = null) : IRequest<Result<ResultadoPagoCliente>>;

/// <summary>
/// Resultado del pago: su número (serie <c>COBRO</c>), el Id de su movimiento de cliente (para aplicarlo) y el número de su
/// registro contable (serie <c>CONTAB</c>).
/// </summary>
public sealed record ResultadoPagoCliente(string Numero, long MovimientoClienteId, string RegistroContable);
