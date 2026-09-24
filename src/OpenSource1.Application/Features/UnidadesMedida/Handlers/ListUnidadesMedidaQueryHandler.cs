using MediatR;
using OpenSource1.Application.Features.UnidadesMedida.Dtos;
using OpenSource1.Application.Features.UnidadesMedida.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.UnidadesMedida.Handlers;

public sealed class ListUnidadesMedidaQueryHandler(IUnidadMedidaReadRepository readRepository)
    : IRequestHandler<ListUnidadesMedidaQuery, Result<PagedResult<UnidadMedidaResponse>>>
{
    public Task<Result<PagedResult<UnidadMedidaResponse>>> Handle(ListUnidadesMedidaQuery request, CancellationToken cancellationToken) =>
        readRepository.ListAsync(request.Search, request.Paginacion, cancellationToken);
}
