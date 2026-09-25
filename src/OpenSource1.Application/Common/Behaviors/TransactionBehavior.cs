using MediatR;
using OpenSource1.Application.Common.Messaging;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Common.Behaviors;

/// <summary>
/// Envuelve cada <see cref="ICommand{TResponse}"/> en una transacción de base de datos.
/// Cualquier otro <c>IRequest</c> (en particular un <see cref="IQuery{TResponse}"/>, o los
/// comandos existentes que todavía no implementan <see cref="ICommand{TResponse}"/>) pasa de
/// largo sin abrir transacción. Si ya hay una transacción activa (comando invocado dentro de
/// otro comando), tampoco abre una nueva: se une a la existente y deja que el ámbito raíz decida
/// el commit o el rollback.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ICommand<TResponse> || unitOfWork.HayTransaccionActiva)
        {
            return await next(cancellationToken);
        }

        await using var ambito = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var respuesta = await next(cancellationToken);

        // Un Result fallido deshace: no se commitea un comando que reportó error después de
        // haber escrito algo (p. ej. "no encontrado" tras un update parcial).
        if (respuesta is Result resultado && resultado.EsFallo)
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            return respuesta;
        }

        await unitOfWork.CommitAsync(cancellationToken);
        return respuesta;
    }
}
