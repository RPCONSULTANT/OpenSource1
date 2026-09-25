using MediatR;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.DiariosInventario.Lineas.Handlers;

public sealed class GetLineaDiarioByIdQueryHandler(ILineaDiarioReadRepository readRepository)
    : IRequestHandler<GetLineaDiarioByIdQuery, Result<LineaDiarioResponse>>
{
    public async Task<Result<LineaDiarioResponse>> Handle(GetLineaDiarioByIdQuery request, CancellationToken cancellationToken)
    {
        var item = await readRepository.GetByIdAsync(request.Id, cancellationToken);

        return item is null
            ? Result<LineaDiarioResponse>.Fallo(new Error(
                "diario_linea.no_encontrado", "No se encontró la línea de diario solicitada.", "Id"))
            : Result<LineaDiarioResponse>.Exito(item);
    }
}
