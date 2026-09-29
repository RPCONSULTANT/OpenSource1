using MediatR;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.NotasCreditoVenta.Posteo;

/// <summary>
/// Posteo de un borrador de nota de crédito de venta (Task 8.6). <see cref="IRequest{TResponse}"/> y no <c>ICommand</c> (mismo
/// criterio que <c>PostearFacturaVentaCommand</c>): el handler abre y confirma él mismo UNA transacción que engloba el documento
/// legal, la devolución de inventario, el libro de clientes (con la aplicación a la factura), el asiento y el borrado del borrador,
/// y lanza <see cref="InvalidOperationException"/> si ya hay una transacción activa.
/// </summary>
public sealed record PostearNotaCreditoVentaCommand(Guid NotaCreditoVentaBorradorId) : IRequest<Result<ResultadoPosteoNotaCredito>>;

/// <summary>
/// Resultado del posteo: número de la nota (serie <c>NC</c>), su total, lo aplicado automáticamente a la factura (0 si la factura ya
/// estaba pagada del todo: la nota queda como saldo a favor) y el número del registro contable (null en una nota de total 0).
/// </summary>
public sealed record ResultadoPosteoNotaCredito(string Numero, decimal ImporteTotal, decimal ImporteAplicado, string? RegistroContable);
