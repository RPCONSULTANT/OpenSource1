using MediatR;
using OpenSource1.Application.Features.NotasCreditoVenta.Posteadas.Dtos;
using OpenSource1.Application.Features.NotasCreditoVenta.Posteadas.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.NotasCreditoVenta.Posteadas.Handlers;

public sealed class ListNotasCreditoVentaQueryHandler(INotaCreditoVentaReadRepository readRepository)
    : IRequestHandler<ListNotasCreditoVentaQuery, Result<PagedResult<NotaCreditoVentaResponse>>>
{
    public async Task<Result<PagedResult<NotaCreditoVentaResponse>>> Handle(ListNotasCreditoVentaQuery request, CancellationToken cancellationToken)
    {
        if (request.Search is { Desde: { } desde, Hasta: { } hasta } && desde > hasta)
        {
            return Result<PagedResult<NotaCreditoVentaResponse>>.Fallo(new Error(
                "nota_credito.rango_fechas_invalido", "La fecha 'desde' no puede ser posterior a 'hasta'.", "Desde"));
        }

        return Result<PagedResult<NotaCreditoVentaResponse>>.Exito(
            await readRepository.ListAsync(request.Search, request.Paginacion, cancellationToken));
    }
}

public sealed class GetNotaCreditoVentaByNumeroQueryHandler(INotaCreditoVentaReadRepository readRepository)
    : IRequestHandler<GetNotaCreditoVentaByNumeroQuery, Result<NotaCreditoVentaDetalleResponse>>
{
    public async Task<Result<NotaCreditoVentaDetalleResponse>> Handle(GetNotaCreditoVentaByNumeroQuery request, CancellationToken cancellationToken)
    {
        var detalle = await readRepository.GetByNumeroAsync(request.Numero, cancellationToken);
        return detalle is null
            ? Result<NotaCreditoVentaDetalleResponse>.Fallo(new Error(
                "nota_credito.no_encontrado", "No se encontró la nota de crédito posteada solicitada.", "Numero"))
            : Result<NotaCreditoVentaDetalleResponse>.Exito(detalle);
    }
}
