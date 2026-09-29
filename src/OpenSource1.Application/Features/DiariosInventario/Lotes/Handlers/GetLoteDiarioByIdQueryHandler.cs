using MediatR;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.DiariosInventario.Lotes.Handlers;

public sealed class GetLoteDiarioByIdQueryHandler(ILoteDiarioReadRepository readRepository)
    : IRequestHandler<GetLoteDiarioByIdQuery, Result<LoteDiarioResponse>>
{
    public async Task<Result<LoteDiarioResponse>> Handle(GetLoteDiarioByIdQuery request, CancellationToken cancellationToken)
    {
        var item = await readRepository.GetByIdAsync(request.Id, cancellationToken);

        return item is null
            ? Result<LoteDiarioResponse>.Fallo(new Error(
                "diario_lote.no_encontrado", "No se encontró el lote de diario solicitado.", "Id"))
            : Result<LoteDiarioResponse>.Exito(item);
    }
}
