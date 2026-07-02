using MediatR;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Application.Features.Productos.Queries;

namespace OpenSource1.Application.Features.Productos.Handlers;

public sealed class ListProductosQueryHandler(IProductoReadRepository readRepository) : IRequestHandler<ListProductosQuery, IReadOnlyList<ProductoResponse>>
{
    public Task<IReadOnlyList<ProductoResponse>> Handle(ListProductosQuery request, CancellationToken cancellationToken) => readRepository.ListAsync(request.Search, cancellationToken);
}
