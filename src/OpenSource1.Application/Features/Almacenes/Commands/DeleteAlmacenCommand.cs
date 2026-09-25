using MediatR;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Almacenes.Commands;

public sealed record DeleteAlmacenCommand(Guid Id) : IRequest<Result>;
