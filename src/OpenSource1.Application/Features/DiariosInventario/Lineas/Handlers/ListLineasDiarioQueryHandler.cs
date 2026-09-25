using MediatR;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Queries;

namespace OpenSource1.Application.Features.DiariosInventario.Lineas.Handlers;

public sealed class ListLineasDiarioQueryHandler(ILineaDiarioReadRepository readRepository)
    : IRequestHandler<ListLineasDiarioQuery, IReadOnlyList<LineaDiarioResponse>>
{
    public Task<IReadOnlyList<LineaDiarioResponse>> Handle(ListLineasDiarioQuery request, CancellationToken cancellationToken) =>
        readRepository.ListByLoteAsync(request.LoteDiarioId, cancellationToken);
}
