using MediatR;

namespace OpenSource1.Application.Common.Messaging;

/// <summary>Escribe. El TransactionBehavior lo envuelve en una transacción.</summary>
public interface ICommand<out TResponse> : IRequest<TResponse>;

/// <summary>Solo lee. No abre transacción.</summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;
