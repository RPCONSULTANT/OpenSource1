using MediatR;

namespace OpenSource1.Application.Features.Productos.Commands;

public sealed record DeleteProductoCommand(Guid Id) : IRequest<bool>;
