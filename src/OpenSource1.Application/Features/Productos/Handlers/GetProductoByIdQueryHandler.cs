using MediatR;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Application.Features.Productos.Queries;

namespace OpenSource1.Application.Features.Productos.Handlers;

public sealed class GetProductoByIdQueryHandler(IProductoReadRepository readRepository) : IRequestHandler<GetProductoByIdQuery, ProductoResponse?>
{
    public Task<ProductoResponse?> Handle(GetProductoByIdQuery request, CancellationToken cancellationToken) => readRepository.GetByIdAsync(request.Id, cancellationToken);
}
