using MediatR;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.DiariosInventario.Lineas.Commands;

public sealed record DeleteLineaDiarioCommand(Guid Id) : IRequest<Result>;
