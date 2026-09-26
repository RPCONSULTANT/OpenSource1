using MediatR;
using OpenSource1.Application.Features.MovimientosCliente.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.MovimientosCliente.Queries;

public sealed record ListMovimientosClienteQuery(MovimientoClienteSearchCriteria Search, PageRequest Paginacion)
    : IRequest<Result<PagedResult<MovimientoClienteResponse>>>;

public sealed record GetSaldoClienteQuery(Guid SocioNegocioId) : IRequest<Result<SaldoClienteResponse>>;

/// <summary>
/// Movimientos ABIERTOS del cliente (restante ≠ 0), sin paginar y en orden cronológico (<c>FechaRegistro</c>, <c>Id</c>): lo que
/// la pantalla de cobros necesita para elegir qué pago aplicar a qué factura (Task 6.5).
/// </summary>
public sealed record ListMovimientosAbiertosClienteQuery(Guid SocioNegocioId) : IRequest<Result<IReadOnlyList<MovimientoClienteResponse>>>;
