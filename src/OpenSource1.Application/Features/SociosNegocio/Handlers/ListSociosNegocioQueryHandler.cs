using MediatR;
using OpenSource1.Application.Features.SociosNegocio.Queries;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.SociosNegocio.Handlers;

public sealed class ListSociosNegocioQueryHandler(ISocioNegocioReadRepository readRepository)
    : IRequestHandler<ListSociosNegocioQuery, Result<PagedResult<SocioNegocioResponse>>>
{
    public Task<Result<PagedResult<SocioNegocioResponse>>> Handle(ListSociosNegocioQuery request, CancellationToken cancellationToken) =>
        readRepository.ListAsync(request.Search, request.Paginacion, cancellationToken);
}
