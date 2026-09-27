using MediatR;
using OpenSource1.Application.Features.Inventario.Consultas.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Inventario.Consultas;

public sealed record ListMovimientosProductoVistaQuery(MovimientoProductoVistaCriterios Criterios, PageRequest Paginacion)
    : IRequest<Result<PagedResult<MovimientoProductoVistaResponse>>>;

public sealed record ListMovimientosValorVistaQuery(MovimientoValorVistaCriterios Criterios, PageRequest Paginacion)
    : IRequest<Result<PagedResult<MovimientoValorVistaResponse>>>;

public sealed record ListExistenciasVistaQuery(ExistenciaVistaCriterios Criterios, PageRequest Paginacion)
    : IRequest<Result<ExistenciasVistaResponse>>;
