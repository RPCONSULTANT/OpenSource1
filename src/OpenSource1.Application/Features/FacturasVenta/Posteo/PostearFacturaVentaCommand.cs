using MediatR;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.FacturasVenta.Posteo;

/// <summary>
/// Posteo de un borrador de factura de venta (spec 6.5, Task 6.4). Es un <see cref="IRequest{TResponse}"/> y NO un
/// <c>ICommand</c> a propósito (mismo criterio que <c>PostearLoteDiarioCommand</c>): el handler abre y confirma él mismo UNA
/// transacción de <c>IUnitOfWork</c> que engloba el documento legal, el inventario, el libro de clientes, el asiento contable y
/// el borrado del borrador, y lanza <see cref="InvalidOperationException"/> si ya hay una transacción activa.
/// <para>Se puede postear un borrador Abierta o Liberada: liberar es un paso opcional de revisión, no un requisito.</para>
/// </summary>
public sealed record PostearFacturaVentaCommand(Guid FacturaVentaBorradorId) : IRequest<Result<ResultadoPosteoFactura>>;

/// <summary>
/// Resultado del posteo: número de la factura (serie <c>FV</c>), su total y el número del registro contable (serie <c>CONTAB</c>).
/// </summary>
public sealed record ResultadoPosteoFactura(string Numero, decimal ImporteTotal, string RegistroContable);
