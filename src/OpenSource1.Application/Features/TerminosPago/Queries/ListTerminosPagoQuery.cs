using MediatR;
using OpenSource1.Application.Features.TerminosPago.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.TerminosPago.Queries;

public sealed record ListTerminosPagoQuery(TerminoPagoSearchCriteria Search, PageRequest Paginacion)
    : IRequest<Result<PagedResult<TerminoPagoResponse>>>;
