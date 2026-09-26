namespace OpenSource1.Application.Features.FacturasVenta.Posteadas.Dtos;

/// <summary>Respuesta de <c>GET api/facturas-venta/{numero}</c>: cabecera, líneas y líneas de IVA.</summary>
public sealed record FacturaVentaDetalleResponse(
    FacturaVentaResponse Cabecera,
    IReadOnlyList<LineaFacturaVentaResponse> Lineas,
    IReadOnlyList<LineaIvaFacturaVentaResponse> LineasIva);
