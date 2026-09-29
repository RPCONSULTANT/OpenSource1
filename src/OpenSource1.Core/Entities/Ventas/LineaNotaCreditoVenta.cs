using OpenSource1.Core.Enums;

namespace OpenSource1.Core.Entities.Ventas;

/// <summary>
/// Línea de una <see cref="NotaCreditoVenta"/> posteada: copia de <see cref="LineaNotaCreditoVentaBorrador"/> más
/// <see cref="MovimientoProductoId"/> (la entrada de devolución, si <see cref="DevolverInventario"/>). Lo ya acreditado de una
/// línea de factura es la suma de <see cref="Cantidad"/> de estas filas con su <see cref="LineaFacturaVentaId"/>. Append-only.
/// </summary>
public sealed class LineaNotaCreditoVenta
{
    public long Id { get; set; }

    /// <summary>varchar(20), FK a <see cref="NotaCreditoVenta.Numero"/>.</summary>
    public required string NotaCreditoVentaNumero { get; set; }

    public int NumeroLinea { get; set; }

    /// <summary>FK a <see cref="LineaFacturaVenta.Id"/>.</summary>
    public long LineaFacturaVentaId { get; set; }

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

    public bool DevolverInventario { get; set; }

    /// <summary>Entrada de devolución (<c>MovimientosProducto</c>, <c>TipoMovimiento = Venta</c>, cantidad positiva); nula sin devolución.</summary>
    public long? MovimientoProductoId { get; set; }
}
