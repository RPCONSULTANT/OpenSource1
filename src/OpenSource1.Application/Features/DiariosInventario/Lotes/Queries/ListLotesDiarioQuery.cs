using MediatR;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.DiariosInventario.Lotes.Queries;

public sealed record ListLotesDiarioQuery(LoteDiarioSearchCriteria Search, PageRequest Paginacion)
    : IRequest<Result<PagedResult<LoteDiarioResponse>>>;
