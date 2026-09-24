using MediatR;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.SociosNegocio.Commands;

public sealed record DeleteSocioNegocioCommand(Guid Id) : IRequest<Result>;
