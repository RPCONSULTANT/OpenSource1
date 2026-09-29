using MediatR;
using OpenSource1.Application.Features.FacturasVenta.Posteadas.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.FacturasVenta.Posteadas.Queries;

public sealed record ListFacturasVentaQuery(FacturaVentaSearchCriteria Search, PageRequest Paginacion)
    : IRequest<Result<PagedResult<FacturaVentaResponse>>>;

public sealed record GetFacturaVentaByNumeroQuery(string Numero) : IRequest<Result<FacturaVentaDetalleResponse>>;
