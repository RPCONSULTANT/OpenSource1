using MediatR;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.DiariosInventario.Registros.Commands;

/// <summary>
/// Registro (posteo) de un lote de diario en el libro de inventario (Task 4.3). Es un <see cref="IRequest{TResponse}"/>
/// y NO un <c>ICommand</c> a propósito: el handler abre y confirma él mismo UNA transacción de <c>IUnitOfWork</c> que
/// engloba el bloqueo del lote y sus líneas, la prevalidación, los bloqueos de producto, el número de la serie, los
/// movimientos, el <c>RegistroDiario</c> y el borrado lógico de las líneas. Si se invocara dentro de una transacción ya
/// abierta, <c>BeginTransactionAsync</c> se une a ella (nunca abre una segunda).
/// </summary>
public sealed record PostearLoteDiarioCommand(Guid LoteDiarioId) : IRequest<Result<ResultadoRegistroLote>>;

/// <summary>
/// Resultado del registro: número asignado, movimientos de producto generados (una transferencia genera dos) y el rango
/// de <c>MovimientosProducto."Id"</c> (min/max).
/// </summary>
public sealed record ResultadoRegistroLote(string NumeroRegistro, int Movimientos, long DesdeMovimientoProducto, long HastaMovimientoProducto);
