using MediatR;
using OpenSource1.Application.Features.FacturasVenta.Borradores.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.FacturasVenta.Borradores.Commands;

/// <summary>
/// Alta de una línea en el borrador de la RUTA (inexistente -&gt; 404). Campos por tipo:
/// <list type="bullet">
/// <item>Producto: <c>ProductoId</c> y <c>Cantidad</c> obligatorios; <c>UnidadMedidaId</c> <see langword="null"/> = la unidad base;
/// <c>AlmacenId</c> <see langword="null"/> = el de la cabecera; <c>PrecioUnitario</c> <see langword="null"/> = <c>PrecioVenta</c> × factor;
/// <c>Descripcion</c> <see langword="null"/> = nombre del producto. <c>CuentaContableId</c>/<c>GrupoIvaProductoId</c> no aplican.</item>
/// <item>CuentaContable: <c>CuentaContableId</c>, <c>GrupoIvaProductoId</c>, <c>Cantidad</c> y <c>PrecioUnitario</c> obligatorios;
/// <c>Descripcion</c> <see langword="null"/> = nombre de la cuenta. <c>ProductoId</c>/<c>AlmacenId</c>/<c>UnidadMedidaId</c> no aplican.</item>
/// <item>Comentario: solo <c>Descripcion</c> (obligatoria); cantidades/precio/descuento nulos o 0.</item>
/// </list>
/// <c>PorcentajeDescuentoLinea</c> <see langword="null"/> = 0. <c>NumeroLinea</c> lo asigna el sistema.
/// </summary>
public sealed record CreateLineaFacturaVentaBorradorCommand(
    Guid FacturaVentaBorradorId,
    TipoLineaFactura Tipo,
    Guid? ProductoId,
    Guid? CuentaContableId,
    string? Descripcion,
    Guid? AlmacenId,
    Guid? UnidadMedidaId,
    decimal? Cantidad,
    decimal? PrecioUnitario,
    decimal? PorcentajeDescuentoLinea,
    Guid? GrupoIvaProductoId) : IRequest<Result<LineaFacturaVentaBorradorResponse>>;

/// <summary>
/// Modificación de una línea: reemplazo completo con los mismos valores por defecto que el alta (una línea es un borrador de
/// un solo paso). <c>FacturaVentaBorradorId</c> y <c>NumeroLinea</c> son inmutables. <c>Xmin</c> obligatorio.
/// </summary>
public sealed record UpdateLineaFacturaVentaBorradorCommand(
    Guid Id,
    TipoLineaFactura Tipo,
    Guid? ProductoId,
    Guid? CuentaContableId,
    string? Descripcion,
    Guid? AlmacenId,
    Guid? UnidadMedidaId,
    decimal? Cantidad,
    decimal? PrecioUnitario,
    decimal? PorcentajeDescuentoLinea,
    Guid? GrupoIvaProductoId,
    long Xmin) : IRequest<Result<LineaFacturaVentaBorradorResponse>>;

public sealed record DeleteLineaFacturaVentaBorradorCommand(Guid Id) : IRequest<Result>;
