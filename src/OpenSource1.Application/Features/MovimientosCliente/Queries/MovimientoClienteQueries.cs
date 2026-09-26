using MediatR;
using OpenSource1.Application.Features.MovimientosCliente.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.MovimientosCliente.Queries;

public sealed record ListMovimientosClienteQuery(MovimientoClienteSearchCriteria Search, PageRequest Paginacion)
    : IRequest<Result<PagedResult<MovimientoClienteResponse>>>;

public sealed record GetSaldoClienteQuery(Guid SocioNegocioId) : IRequest<Result<SaldoClienteResponse>>;
