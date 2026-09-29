using MediatR;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Productos.Commands;

public sealed record DeleteProductoCommand(Guid Id) : IRequest<Result>;
