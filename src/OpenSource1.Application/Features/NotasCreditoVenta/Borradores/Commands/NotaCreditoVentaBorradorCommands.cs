using MediatR;
using OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Commands;

/// <summary>
/// Alta de un borrador de nota de crédito a partir de una factura POSTEADA (<c>FacturaVentaNumero</c> obligatorio): copia de la
/// factura los socios, el snapshot de facturación, los grupos y la CxC congelada. <c>FechaRegistro</c> <see langword="null"/> = hoy
/// (UTC), nunca anterior a la de la factura; <c>FechaDocumento</c> <see langword="null"/> = <c>FechaRegistro</c>. Con
/// <c>CopiarLineas</c>, crea una línea por cada línea de la factura con cantidad pendiente de acreditar, por todo lo pendiente
/// (nota total), con <c>DevolverInventario</c> en las de Producto. El número sale de la serie <c>NC-BORR</c>.
/// </summary>
public sealed record CreateNotaCreditoVentaBorradorCommand(
    string FacturaVentaNumero,
    DateOnly? FechaRegistro,
    DateOnly? FechaDocumento,
    string? Descripcion,
    bool CopiarLineas = false,
    bool DevolverInventario = false) : IRequest<Result<NotaCreditoVentaBorradorResponse>>;

/// <summary>
/// Modificación de la cabecera: solo fechas y descripción ("null = conservar"; <c>Descripcion</c> "" = limpiar). La factura, los
/// socios y los grupos no cambian. <c>Xmin</c> obligatorio (409 si no coincide).
/// </summary>
public sealed record UpdateNotaCreditoVentaBorradorCommand(
    Guid Id, DateOnly? FechaRegistro, DateOnly? FechaDocumento, string? Descripcion, long Xmin)
    : IRequest<Result<NotaCreditoVentaBorradorResponse>>;

/// <summary>Borrado lógico del borrador y de todas sus líneas.</summary>
public sealed record DeleteNotaCreditoVentaBorradorCommand(Guid Id) : IRequest<Result>;

/// <summary>
/// Alta de una línea en el borrador de la RUTA (inexistente -&gt; 404) que acredita <c>Cantidad</c> de la línea
/// <c>LineaFacturaVentaId</c> de su factura (una sola línea de nota por línea de factura). <c>DevolverInventario</c> solo en
/// líneas de Producto.
/// </summary>
public sealed record CreateLineaNotaCreditoVentaBorradorCommand(
    Guid NotaCreditoVentaBorradorId, long LineaFacturaVentaId, decimal? Cantidad, bool DevolverInventario)
    : IRequest<Result<LineaNotaCreditoVentaBorradorResponse>>;

/// <summary>Modificación de la cantidad y la devolución de una línea (lo demás es de la factura). <c>Xmin</c> obligatorio.</summary>
public sealed record UpdateLineaNotaCreditoVentaBorradorCommand(Guid Id, decimal? Cantidad, bool DevolverInventario, long Xmin)
    : IRequest<Result<LineaNotaCreditoVentaBorradorResponse>>;

public sealed record DeleteLineaNotaCreditoVentaBorradorCommand(Guid Id) : IRequest<Result>;
