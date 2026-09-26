using MediatR;
using OpenSource1.Application.Features.Contabilidad.Dtos;
using OpenSource1.Application.Features.Contabilidad.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Contabilidad.Handlers;

public sealed class ListMovimientosContablesQueryHandler(IContabilidadReadRepository readRepository)
    : IRequestHandler<ListMovimientosContablesQuery, Result<PagedResult<MovimientoContableResponse>>>
{
    public Task<Result<PagedResult<MovimientoContableResponse>>> Handle(
        ListMovimientosContablesQuery request, CancellationToken cancellationToken)
    {
        if (request.Search is { Desde: { } desde, Hasta: { } hasta } && desde > hasta)
        {
            return Task.FromResult(Result<PagedResult<MovimientoContableResponse>>.Fallo(new Error(
                "contabilidad.rango_fechas_invalido", "La fecha 'desde' no puede ser posterior a 'hasta'.", "Desde")));
        }

        return readRepository.ListMovimientosAsync(request.Search, request.Paginacion, cancellationToken);
    }
}
