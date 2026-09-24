using MediatR;
using OpenSource1.Application.Features.CategoriasProducto.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.CategoriasProducto.Queries;

public sealed record ListCategoriasProductoQuery(CategoriaProductoSearchCriteria Search, PageRequest Paginacion)
    : IRequest<Result<PagedResult<CategoriaProductoResponse>>>;
