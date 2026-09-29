using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.FacturasVenta.Borradores;

public sealed record FacturaVentaBorradorSearchCriteria(
    string? Numero, string? NombreFacturacion, Guid? SocioNegocioId, EstadoFacturaBorrador? Estado);
