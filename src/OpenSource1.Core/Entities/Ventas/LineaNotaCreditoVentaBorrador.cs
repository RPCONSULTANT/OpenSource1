using OpenSource1.Core.Enums;

namespace OpenSource1.Core.Entities.Ventas;

/// <summary>
/// Línea de un <see cref="NotaCreditoVentaBorrador"/> (Task 8.6): acredita <see cref="Cantidad"/> de UNA línea de la factura
/// (<see cref="LineaFacturaVentaId"/>, de tipo Producto o CuentaContable; una sola línea de nota por línea de factura en cada
/// borrador). Copia de la línea original, sin posibilidad de editarlos, el número de línea, el tipo, las referencias, el precio, el
/// descuento, el IVA congelado (identificador y porcentaje) y los grupos; solo se editan <see cref="Cantidad"/> (≤ facturada − ya
/// acreditada por notas POSTEADAS) y <see cref="DevolverInventario"/> (solo Producto). Maestro con soft delete y <c>xmin</c>.
/// </summary>
public sealed class LineaNotaCreditoVentaBorrador : BaseEntity
{
    public Guid NotaCreditoVentaBorradorId { get; set; }

    /// <summary>FK a <see cref="LineaFacturaVenta.Id"/>.</summary>
    public long LineaFacturaVentaId { get; set; }

    /// <summary>El de la línea de la factura (único por borrador, como la línea de factura).</summary>
    public int NumeroLinea { get; set; }

    /// <summary>Producto o CuentaContable (los comentarios no se acreditan).</summary>
    public TipoLineaFactura Tipo { get; set; }

    public Guid? ProductoId { get; set; }
    public Guid? CuentaContableId { get; set; }
    public string? Descripcion { get; set; }
    public Guid? AlmacenId { get; set; }
    public Guid? UnidadMedidaId { get; set; }

    /// <summary>Factor CONGELADO en la factura: la devolución se convierte con él, no con el vigente.</summary>
    public decimal CantidadPorUnidadMedida { get; set; }

    /// <summary>&gt; 0, en la unidad de la línea de la factura.</summary>
    public decimal Cantidad { get; set; }

    public decimal PrecioUnitario { get; set; }
    public decimal PorcentajeDescuentoLinea { get; set; }

    /// <summary><c>ROUND(Cantidad × PrecioUnitario × PorcentajeDescuentoLinea / 100, 2)</c>.</summary>
    public decimal ImporteDescuentoLinea { get; set; }

    /// <summary><c>ROUND(Cantidad × PrecioUnitario, 2) − ImporteDescuentoLinea</c>.</summary>
    public decimal ImporteLinea { get; set; }

    public Guid? GrupoProductoId { get; set; }
    public Guid? GrupoIvaProductoId { get; set; }
    public Guid? GrupoInventarioId { get; set; }

    public string? IdentificadorIva { get; set; }
    public decimal PorcentajeIva { get; set; }

    /// <summary>Solo en líneas de Producto: al postear, entrada de inventario al costo de la salida original.</summary>
    public bool DevolverInventario { get; set; }
}
