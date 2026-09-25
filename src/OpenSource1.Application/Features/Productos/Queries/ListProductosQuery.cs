using MediatR;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Productos.Queries;

public sealed record ListProductosQuery(ProductoSearchCriteria Search, PageRequest Paginacion)
    : IRequest<Result<PagedResult<ProductoResponse>>>;
