using MediatR;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.CategoriasProducto.Commands;

public sealed record DeleteCategoriaProductoCommand(Guid Id) : IRequest<Result>;
