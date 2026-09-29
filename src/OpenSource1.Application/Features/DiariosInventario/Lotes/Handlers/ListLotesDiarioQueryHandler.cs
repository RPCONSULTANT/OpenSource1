using MediatR;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.DiariosInventario.Lotes.Handlers;

public sealed class ListLotesDiarioQueryHandler(ILoteDiarioReadRepository readRepository)
    : IRequestHandler<ListLotesDiarioQuery, Result<PagedResult<LoteDiarioResponse>>>
{
    public Task<Result<PagedResult<LoteDiarioResponse>>> Handle(ListLotesDiarioQuery request, CancellationToken cancellationToken) =>
        readRepository.ListAsync(request.Search, request.Paginacion, cancellationToken);
}
