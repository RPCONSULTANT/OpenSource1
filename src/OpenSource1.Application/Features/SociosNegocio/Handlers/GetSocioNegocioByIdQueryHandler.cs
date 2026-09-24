using MediatR;
using OpenSource1.Application.Features.SociosNegocio.Queries;
using OpenSource1.Application.Features.SociosNegocio.Dtos;

namespace OpenSource1.Application.Features.SociosNegocio.Handlers;

public sealed class GetSocioNegocioByIdQueryHandler(ISocioNegocioReadRepository readRepository) : IRequestHandler<GetSocioNegocioByIdQuery, SocioNegocioResponse?>
{
    public Task<SocioNegocioResponse?> Handle(GetSocioNegocioByIdQuery request, CancellationToken cancellationToken) => readRepository.GetByIdAsync(request.Id, cancellationToken);
}
