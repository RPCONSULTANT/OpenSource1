using MediatR;
using OpenSource1.Application.Features.Almacenes.Dtos;
using OpenSource1.Application.Features.Almacenes.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Almacenes.Handlers;

public sealed class ListAlmacenesQueryHandler(IAlmacenReadRepository readRepository)
    : IRequestHandler<ListAlmacenesQuery, Result<PagedResult<AlmacenResponse>>>
{
    public Task<Result<PagedResult<AlmacenResponse>>> Handle(ListAlmacenesQuery request, CancellationToken cancellationToken) =>
        readRepository.ListAsync(request.Search, request.Paginacion, cancellationToken);
}
