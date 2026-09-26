using MediatR;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Contabilidad.Commands;

/// <summary>
/// Ejecuta el batch "Postear costo de inventario a contabilidad" (Task 5.6) para todos los movimientos de valor pendientes o
/// solo los de <paramref name="ProductoId"/>. Es un <see cref="IRequest{TResponse}"/> y NO un <c>ICommand</c> a propósito: el
/// <c>TransactionBehavior</c> lo envolvería en una única transacción, y el batch abre y confirma una transacción POR
/// (producto, fecha) para que un producto sin setup no bloquee a los demás (mismo criterio que
/// <c>AjustarCostoMovimientosCommand</c>).
/// </summary>
public sealed record PostearCostoInventarioCommand(Guid? ProductoId) : IRequest<Result<ResultadoPosteoCostoInventario>>;
