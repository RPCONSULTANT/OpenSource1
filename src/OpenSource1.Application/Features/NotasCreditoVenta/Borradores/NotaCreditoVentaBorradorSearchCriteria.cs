namespace OpenSource1.Application.Features.NotasCreditoVenta.Borradores;

/// <summary>Filtros de los borradores: número y factura (texto con la sintaxis de <c>FilterExpressionBuilder</c>) y socio (vender-a o facturar-a).</summary>
public sealed record NotaCreditoVentaBorradorSearchCriteria(
    string? Numero = null, string? FacturaVentaNumero = null, string? NombreFacturacion = null, Guid? SocioNegocioId = null);
