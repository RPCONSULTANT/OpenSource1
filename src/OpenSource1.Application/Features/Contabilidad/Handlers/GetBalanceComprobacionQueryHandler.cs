using MediatR;
using OpenSource1.Application.Features.Contabilidad.Dtos;
using OpenSource1.Application.Features.Contabilidad.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Contabilidad.Handlers;

public sealed class GetBalanceComprobacionQueryHandler(IContabilidadReadRepository readRepository)
    : IRequestHandler<GetBalanceComprobacionQuery, Result<BalanceComprobacionResponse>>
{
    public async Task<Result<BalanceComprobacionResponse>> Handle(GetBalanceComprobacionQuery request, CancellationToken cancellationToken)
    {
        if (request.Criterios is { Desde: { } desde, Hasta: { } hasta } && desde > hasta)
        {
            return Result<BalanceComprobacionResponse>.Fallo(ContabilidadErrores.RangoFechasInvalido());
        }

        return Result<BalanceComprobacionResponse>.Exito(await readRepository.GetBalanceComprobacionAsync(request.Criterios, cancellationToken));
    }
}
