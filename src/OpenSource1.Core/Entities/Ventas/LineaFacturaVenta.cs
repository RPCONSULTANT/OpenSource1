using OpenSource1.Core.Enums;

namespace OpenSource1.Core.Entities.Ventas;

/// <summary>
/// Línea de una <see cref="FacturaVenta"/> posteada (spec 6.2): copia de <see cref="LineaFacturaVentaBorrador"/> sin <c>xmin</c>
/// ni soft delete, con los valores congelados del borrador, más <see cref="MovimientoProductoId"/> (la salida de inventario de
/// las líneas de Producto). Append-only (mismo trigger que el libro), sin <see cref="BaseEntity"/> ni <c>IAggregateRoot</c>.
/// </summary>
public sealed class LineaFacturaVenta
{
    public long Id { get; set; }

    /// <summary>varchar(20), FK a <see cref="FacturaVenta.Numero"/>.</summary>
    public required string FacturaVentaNumero { get; set; }

    /// <summary>El del borrador (único por factura).</summary>
    public int NumeroLinea { get; set; }

    public TipoLineaFactura Tipo { get; set; }

    public Guid? ProductoId { get; set; }
    public Guid? CuentaContableId { get; set; }

    public string? Descripcion { get; set; }

    public Guid? AlmacenId { get; set; }
    public Guid? UnidadMedidaId { get; set; }

    public decimal CantidadPorUnidadMedida { get; set; }
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal PorcentajeDescuentoLinea { get; set; }
    public decimal ImporteDescuentoLinea { get; set; }
    public decimal ImporteLinea { get; set; }

    public Guid? GrupoProductoId { get; set; }
    public Guid? GrupoIvaProductoId { get; set; }
    public Guid? GrupoInventarioId { get; set; }

    public string? IdentificadorIva { get; set; }
    public decimal PorcentajeIva { get; set; }

    /// <summary>Salida de inventario (<c>MovimientosProducto</c>) de una línea de Producto; nulo en las demás.</summary>
    public long? MovimientoProductoId { get; set; }
}
