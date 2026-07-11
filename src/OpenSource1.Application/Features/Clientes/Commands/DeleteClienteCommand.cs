using MediatR;

namespace OpenSource1.Application.Features.Clientes.Commands;

public sealed record DeleteClienteCommand(Guid Id) : IRequest<bool>;
