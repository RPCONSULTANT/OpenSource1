using OpenSource1.Application.Features.FacturasVenta.Calculo;

namespace OpenSource1.Blazor.Components.Documento;

/// <summary>
/// Un dato de la cabecera de un documento en solo lectura (spec no-series, Parte 4): <c>Href</c> lo convierte en enlace,
/// <c>Detalle</c> es una segunda línea tenue (RNC, dirección…) y <c>Ancho</c> ocupa toda la fila (descripción).
/// </summary>
public sealed record CampoDocumento(string Etiqueta, string Valor, string? Href = null, string? Detalle = null, bool Ancho = false);

/// <summary>
/// Totales al pie del documento: subtotal (antes de descuentos), descuentos, importe sin ITBIS, ITBIS y total, con el IVA agrupado
/// por identificador. <see cref="Desde"/> los arma desde los <see cref="TotalesFactura"/> de la API (vista previa del borrador o
/// factura/nota posteada) y la suma de descuentos de las líneas.
/// </summary>
public sealed record TotalesDocumento(
    decimal Subtotal, decimal Descuentos, decimal ImporteSinIva, decimal Itbis, decimal Total, IReadOnlyList<GrupoIvaCalculado> Grupos)
{
    public static TotalesDocumento Desde(TotalesFactura totales, decimal descuentos) =>
        new(totales.ImporteSinIva + descuentos, descuentos, totales.ImporteSinIva, totales.ImporteIva, totales.ImporteTotal, totales.Grupos);
}
