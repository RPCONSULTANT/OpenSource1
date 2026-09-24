using MediatR;

namespace OpenSource1.Application.Features.SociosNegocio.Commands;

public sealed record DeleteSocioNegocioCommand(Guid Id) : IRequest<bool>;
