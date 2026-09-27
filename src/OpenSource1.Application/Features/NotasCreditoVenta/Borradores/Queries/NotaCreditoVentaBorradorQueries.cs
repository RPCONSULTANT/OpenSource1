using MediatR;
using OpenSource1.Application.Features.FacturasVenta.Calculo;
using OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Queries;

public sealed record GetNotaCreditoVentaBorradorByIdQuery(Guid Id) : IRequest<Result<NotaCreditoVentaBorradorResponse>>;

public sealed record ListNotasCreditoVentaBorradorQuery(NotaCreditoVentaBorradorSearchCriteria Search, PageRequest Paginacion)
    : IRequest<Result<PagedResult<NotaCreditoVentaBorradorResponse>>>;

/// <summary>Todas las líneas vivas del borrador, por <c>NumeroLinea</c>. Borrador inexistente -&gt; 404.</summary>
public sealed record ListLineasNotaCreditoVentaBorradorQuery(Guid NotaCreditoVentaBorradorId)
    : IRequest<Result<IReadOnlyList<LineaNotaCreditoVentaBorradorResponse>>>;

public sealed record GetLineaNotaCreditoVentaBorradorByIdQuery(Guid Id) : IRequest<Result<LineaNotaCreditoVentaBorradorResponse>>;

/// <summary>Líneas de la factura que el borrador puede acreditar ("ofrece sus líneas"). Borrador inexistente -&gt; 404.</summary>
public sealed record ListLineasAcreditablesNotaCreditoVentaQuery(Guid NotaCreditoVentaBorradorId)
    : IRequest<Result<IReadOnlyList<LineaFacturaAcreditableResponse>>>;

/// <summary>Vista previa de los totales con <see cref="CalculadoraIvaFactura"/>. Borrador inexistente -&gt; 404.</summary>
public sealed record GetTotalesNotaCreditoVentaBorradorQuery(Guid NotaCreditoVentaBorradorId) : IRequest<Result<TotalesFactura>>;
