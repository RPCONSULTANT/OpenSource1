using MediatR;
using OpenSource1.Application.Features.Productos.Dtos;

namespace OpenSource1.Application.Features.Productos.Queries;

public sealed record ListProductosQuery(ProductoSearchCriteria Search) : IRequest<IReadOnlyList<ProductoResponse>>;
