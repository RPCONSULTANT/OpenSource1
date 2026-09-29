using MediatR;
using OpenSource1.Application.Features.TerminosPago.Dtos;
using OpenSource1.Application.Features.TerminosPago.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.TerminosPago.Handlers;

public sealed class GetTerminoPagoByIdQueryHandler(ITerminoPagoReadRepository readRepository)
    : IRequestHandler<GetTerminoPagoByIdQuery, Result<TerminoPagoResponse>>
{
    public async Task<Result<TerminoPagoResponse>> Handle(GetTerminoPagoByIdQuery request, CancellationToken cancellationToken)
    {
        var item = await readRepository.GetByIdAsync(request.Id, cancellationToken);

        return item is null
            ? Result<TerminoPagoResponse>.Fallo(new Error(
                "termino_pago.no_encontrado", "No se encontró el término de pago solicitado.", "Id"))
            : Result<TerminoPagoResponse>.Exito(item);
    }
}
