using MediatR;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Application.Features.Productos.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Productos.Handlers;

public sealed class ListProductosQueryHandler(IProductoReadRepository readRepository)
    : IRequestHandler<ListProductosQuery, Result<PagedResult<ProductoResponse>>>
{
    public Task<Result<PagedResult<ProductoResponse>>> Handle(ListProductosQuery request, CancellationToken cancellationToken) =>
        readRepository.ListAsync(request.Search, request.Paginacion, cancellationToken);
}
