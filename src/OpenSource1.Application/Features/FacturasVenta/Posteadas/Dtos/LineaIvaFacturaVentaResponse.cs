namespace OpenSource1.Application.Features.FacturasVenta.Posteadas.Dtos;

/// <summary>Línea de IVA (un grupo por identificador) de una factura posteada, con el número ACTUAL de la cuenta de IVA.</summary>
public sealed class LineaIvaFacturaVentaResponse
{
    public long Id { get; init; }
    public string IdentificadorIva { get; init; } = string.Empty;
    public decimal PorcentajeIva { get; init; }
    public decimal BaseImponible { get; init; }
    public decimal ImporteIva { get; init; }
    public Guid CuentaIvaId { get; init; }
    public string? CuentaIvaNumero { get; init; }
}
