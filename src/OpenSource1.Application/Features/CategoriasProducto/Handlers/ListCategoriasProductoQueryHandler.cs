using MediatR;
using OpenSource1.Application.Features.CategoriasProducto.Dtos;
using OpenSource1.Application.Features.CategoriasProducto.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.CategoriasProducto.Handlers;

public sealed class ListCategoriasProductoQueryHandler(ICategoriaProductoReadRepository readRepository)
    : IRequestHandler<ListCategoriasProductoQuery, Result<PagedResult<CategoriaProductoResponse>>>
{
    public Task<Result<PagedResult<CategoriaProductoResponse>>> Handle(ListCategoriasProductoQuery request, CancellationToken cancellationToken) =>
        readRepository.ListAsync(request.Search, request.Paginacion, cancellationToken);
}
