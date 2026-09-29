namespace OpenSource1.Application.Features.FacturasVenta.Posteadas;

/// <summary>
/// Filtros de <c>GET api/facturas-venta</c> (facturas posteadas): número y nombre de facturación (texto con la sintaxis de
/// <c>FilterExpressionBuilder</c>), socio (vender-a o facturar-a) y rango de <c>FechaRegistro</c> (ambos extremos incluidos).
/// </summary>
public sealed record FacturaVentaSearchCriteria(
    string? Numero = null,
    string? NombreFacturacion = null,
    Guid? SocioNegocioId = null,
    DateOnly? Desde = null,
    DateOnly? Hasta = null);
