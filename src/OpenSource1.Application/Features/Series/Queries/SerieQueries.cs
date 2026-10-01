using MediatR;
using OpenSource1.Application.Features.Series.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Series.Queries;

public sealed record ListSeriesQuery(SerieSearchCriteria Search, PageRequest Paginacion) : IRequest<Result<PagedResult<SerieResponse>>>;

public sealed record GetSerieByIdQuery(Guid Id) : IRequest<Result<SerieDetalleResponse>>;

public sealed record GetProximoNumeroQuery(Guid SerieId, DateOnly? Fecha) : IRequest<Result<ProximoNumeroResponse>>;
