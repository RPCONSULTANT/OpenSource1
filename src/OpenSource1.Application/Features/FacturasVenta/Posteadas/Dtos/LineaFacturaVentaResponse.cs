using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.FacturasVenta.Posteadas.Dtos;

/// <summary>Línea de una factura posteada, con los valores congelados al postear y los códigos resueltos por JOIN.</summary>
public sealed class LineaFacturaVentaResponse
{
    public long Id { get; init; }
    public string FacturaVentaNumero { get; init; } = string.Empty;
    public int NumeroLinea { get; init; }
    public TipoLineaFactura Tipo { get; init; }
    public Guid? ProductoId { get; init; }
    public string? ProductoCodigo { get; init; }
    public Guid? CuentaContableId { get; init; }
    public string? CuentaContableNumero { get; init; }
    public string? Descripcion { get; init; }
    public Guid? AlmacenId { get; init; }
    public string? AlmacenCodigo { get; init; }
    public Guid? UnidadMedidaId { get; init; }
    public string? UnidadMedidaCodigo { get; init; }
    public decimal CantidadPorUnidadMedida { get; init; }
    public decimal Cantidad { get; init; }
    public decimal PrecioUnitario { get; init; }
    public decimal PorcentajeDescuentoLinea { get; init; }
    public decimal ImporteDescuentoLinea { get; init; }
    public decimal ImporteLinea { get; init; }
    public Guid? GrupoProductoId { get; init; }
    public Guid? GrupoIvaProductoId { get; init; }
    public Guid? GrupoInventarioId { get; init; }
    public string? IdentificadorIva { get; init; }
    public decimal PorcentajeIva { get; init; }
    public long? MovimientoProductoId { get; init; }
}
