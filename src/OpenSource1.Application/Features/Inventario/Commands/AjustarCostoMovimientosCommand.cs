using MediatR;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Inventario.Commands;

/// <summary>
/// Ejecuta la rutina "Ajustar costo movimientos" para los productos pendientes (<c>CostoAjustado = false</c>) o solo para
/// <paramref name="ProductoId"/>. Es un <see cref="IRequest{TResponse}"/> y NO un <c>ICommand</c> a propósito: el
/// <c>TransactionBehavior</c> lo envolvería en una única transacción, y la rutina abre y confirma una transacción POR
/// producto (un producto con un error no deshace los ya ajustados, y el bloqueo de cada producto dura lo mínimo).
/// </summary>
public sealed record AjustarCostoMovimientosCommand(Guid? ProductoId) : IRequest<Result<ResultadoAjusteCosto>>;
