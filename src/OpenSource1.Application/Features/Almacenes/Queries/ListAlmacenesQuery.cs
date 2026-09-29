using MediatR;
using OpenSource1.Application.Features.Almacenes.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Almacenes.Queries;

public sealed record ListAlmacenesQuery(AlmacenSearchCriteria Search, PageRequest Paginacion)
    : IRequest<Result<PagedResult<AlmacenResponse>>>;
