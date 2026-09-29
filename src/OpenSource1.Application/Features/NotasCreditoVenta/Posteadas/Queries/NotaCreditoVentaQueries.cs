using MediatR;
using OpenSource1.Application.Features.NotasCreditoVenta.Posteadas.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.NotasCreditoVenta.Posteadas.Queries;

public sealed record ListNotasCreditoVentaQuery(NotaCreditoVentaSearchCriteria Search, PageRequest Paginacion)
    : IRequest<Result<PagedResult<NotaCreditoVentaResponse>>>;

public sealed record GetNotaCreditoVentaByNumeroQuery(string Numero) : IRequest<Result<NotaCreditoVentaDetalleResponse>>;
