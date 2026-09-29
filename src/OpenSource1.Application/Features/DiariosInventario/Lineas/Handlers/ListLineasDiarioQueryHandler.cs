using MediatR;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Queries;
using OpenSource1.Application.Features.DiariosInventario.Lotes;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.DiariosInventario.Lineas.Handlers;

public sealed class ListLineasDiarioQueryHandler(ILoteDiarioReadRepository loteReadRepository, ILineaDiarioReadRepository readRepository)
    : IRequestHandler<ListLineasDiarioQuery, Result<IReadOnlyList<LineaDiarioResponse>>>
{
    public async Task<Result<IReadOnlyList<LineaDiarioResponse>>> Handle(ListLineasDiarioQuery request, CancellationToken cancellationToken)
    {
        var lote = await loteReadRepository.GetByIdAsync(request.LoteDiarioId, cancellationToken);
        if (lote is null)
        {
            return Result<IReadOnlyList<LineaDiarioResponse>>.Fallo(new Error(
                "diario_lote.no_encontrado", "No se encontró el lote de diario solicitado.", "LoteDiarioId"));
        }

        var lineas = await readRepository.ListByLoteAsync(request.LoteDiarioId, cancellationToken);
        return Result<IReadOnlyList<LineaDiarioResponse>>.Exito(lineas);
    }
}
