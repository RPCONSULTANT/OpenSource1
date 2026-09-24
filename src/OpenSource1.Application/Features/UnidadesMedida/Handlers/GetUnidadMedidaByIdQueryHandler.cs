using MediatR;
using OpenSource1.Application.Features.UnidadesMedida.Dtos;
using OpenSource1.Application.Features.UnidadesMedida.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.UnidadesMedida.Handlers;

public sealed class GetUnidadMedidaByIdQueryHandler(IUnidadMedidaReadRepository readRepository)
    : IRequestHandler<GetUnidadMedidaByIdQuery, Result<UnidadMedidaResponse>>
{
    public async Task<Result<UnidadMedidaResponse>> Handle(GetUnidadMedidaByIdQuery request, CancellationToken cancellationToken)
    {
        var item = await readRepository.GetByIdAsync(request.Id, cancellationToken);

        return item is null
            ? Result<UnidadMedidaResponse>.Fallo(new Error(
                "unidad_medida.no_encontrado", "No se encontró la unidad de medida solicitada.", "Id"))
            : Result<UnidadMedidaResponse>.Exito(item);
    }
}
