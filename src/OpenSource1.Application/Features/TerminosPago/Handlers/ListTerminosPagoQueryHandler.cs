using MediatR;
using OpenSource1.Application.Features.TerminosPago.Dtos;
using OpenSource1.Application.Features.TerminosPago.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.TerminosPago.Handlers;

public sealed class ListTerminosPagoQueryHandler(ITerminoPagoReadRepository readRepository)
    : IRequestHandler<ListTerminosPagoQuery, Result<PagedResult<TerminoPagoResponse>>>
{
    public Task<Result<PagedResult<TerminoPagoResponse>>> Handle(ListTerminosPagoQuery request, CancellationToken cancellationToken) =>
        readRepository.ListAsync(request.Search, request.Paginacion, cancellationToken);
}
