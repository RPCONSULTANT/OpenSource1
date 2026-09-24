using MediatR;
using OpenSource1.Application.Features.SociosNegocio.Dtos;

namespace OpenSource1.Application.Features.SociosNegocio.Queries;

public sealed record GetSocioNegocioByIdQuery(Guid Id) : IRequest<SocioNegocioResponse?>;
