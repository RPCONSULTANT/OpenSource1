using MediatR;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.DiariosInventario.Lotes.Commands;

public sealed record DeleteLoteDiarioCommand(Guid Id) : IRequest<Result>;
