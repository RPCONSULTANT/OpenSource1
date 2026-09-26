using MediatR;
using OpenSource1.Application.Features.FacturasVenta.Posteadas.Dtos;
using OpenSource1.Application.Features.FacturasVenta.Posteadas.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.FacturasVenta.Posteadas.Handlers;

public sealed class ListFacturasVentaQueryHandler(IFacturaVentaReadRepository readRepository)
    : IRequestHandler<ListFacturasVentaQuery, Result<PagedResult<FacturaVentaResponse>>>
{
    public async Task<Result<PagedResult<FacturaVentaResponse>>> Handle(ListFacturasVentaQuery request, CancellationToken cancellationToken)
    {
        if (request.Search is { Desde: { } desde, Hasta: { } hasta } && desde > hasta)
        {
            return Result<PagedResult<FacturaVentaResponse>>.Fallo(new Error(
                "factura.rango_fechas_invalido", "La fecha 'desde' no puede ser posterior a 'hasta'.", "Desde"));
        }

        return Result<PagedResult<FacturaVentaResponse>>.Exito(
            await readRepository.ListAsync(request.Search, request.Paginacion, cancellationToken));
    }
}

public sealed class GetFacturaVentaByNumeroQueryHandler(IFacturaVentaReadRepository readRepository)
    : IRequestHandler<GetFacturaVentaByNumeroQuery, Result<FacturaVentaDetalleResponse>>
{
    public async Task<Result<FacturaVentaDetalleResponse>> Handle(GetFacturaVentaByNumeroQuery request, CancellationToken cancellationToken)
    {
        var detalle = await readRepository.GetByNumeroAsync(request.Numero, cancellationToken);
        return detalle is null
            ? Result<FacturaVentaDetalleResponse>.Fallo(new Error(
                "factura.no_encontrado", "No se encontró la factura de venta posteada solicitada.", "Numero"))
            : Result<FacturaVentaDetalleResponse>.Exito(detalle);
    }
}
