using MediatR;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.SociosNegocio.Queries;

public sealed record GetSocioNegocioByIdQuery(Guid Id) : IRequest<Result<SocioNegocioResponse>>;
