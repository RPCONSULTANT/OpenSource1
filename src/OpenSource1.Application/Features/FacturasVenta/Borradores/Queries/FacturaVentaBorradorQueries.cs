using MediatR;
using OpenSource1.Application.Features.FacturasVenta.Borradores.Dtos;
using OpenSource1.Application.Features.FacturasVenta.Calculo;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.FacturasVenta.Borradores.Queries;

public sealed record GetFacturaVentaBorradorByIdQuery(Guid Id) : IRequest<Result<FacturaVentaBorradorResponse>>;

public sealed record ListFacturasVentaBorradorQuery(FacturaVentaBorradorSearchCriteria Search, PageRequest Paginacion)
    : IRequest<Result<PagedResult<FacturaVentaBorradorResponse>>>;

/// <summary>Todas las líneas vivas del borrador, por <c>NumeroLinea</c>. Borrador inexistente -&gt; 404.</summary>
public sealed record ListLineasFacturaVentaBorradorQuery(Guid FacturaVentaBorradorId)
    : IRequest<Result<IReadOnlyList<LineaFacturaVentaBorradorResponse>>>;

public sealed record GetLineaFacturaVentaBorradorByIdQuery(Guid Id) : IRequest<Result<LineaFacturaVentaBorradorResponse>>;

/// <summary>Vista previa de los totales (derivados, D1) con <see cref="CalculadoraIvaFactura"/>. Borrador inexistente -&gt; 404.</summary>
public sealed record GetTotalesFacturaVentaBorradorQuery(Guid FacturaVentaBorradorId) : IRequest<Result<TotalesFactura>>;
