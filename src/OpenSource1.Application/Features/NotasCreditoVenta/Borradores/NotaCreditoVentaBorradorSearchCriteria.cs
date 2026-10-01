using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.NotasCreditoVenta.Borradores;

/// <summary>
/// Filtros de los borradores: número y factura (texto con la sintaxis de <c>FilterExpressionBuilder</c>), socio (vender-a o
/// facturar-a) y estado (<see langword="null"/> = <see cref="EstadoNotaCreditoBorrador.Abierta"/>).
/// </summary>
public sealed record NotaCreditoVentaBorradorSearchCriteria(
    string? Numero = null, string? FacturaVentaNumero = null, string? NombreFacturacion = null, Guid? SocioNegocioId = null,
    EstadoNotaCreditoBorrador? Estado = null);
