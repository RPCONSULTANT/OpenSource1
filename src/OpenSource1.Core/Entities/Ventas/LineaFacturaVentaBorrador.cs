using OpenSource1.Core.Enums;

namespace OpenSource1.Core.Entities.Ventas;

/// <summary>
/// Línea de un <see cref="FacturaVentaBorrador"/> (spec 6.1). Maestro con soft delete y concurrencia optimista. Congela
/// al guardarse el factor de la unidad, los grupos del producto y el IVA (<see cref="IdentificadorIva"/>,
/// <see cref="PorcentajeIva"/>) derivado de (GrupoIvaNegocio de la cabecera × <see cref="GrupoIvaProductoId"/>).
/// <list type="bullet">
/// <item><see cref="TipoLineaFactura.Producto"/>: <see cref="ProductoId"/>, <see cref="AlmacenId"/> y <see cref="UnidadMedidaId"/>
/// obligatorios; los tres grupos del producto congelados.</item>
/// <item><see cref="TipoLineaFactura.CuentaContable"/>: <see cref="CuentaContableId"/> y <see cref="GrupoIvaProductoId"/> obligatorios;
/// sin almacén ni unidad (factor 1).</item>
/// <item><see cref="TipoLineaFactura.Comentario"/>: solo <see cref="Descripcion"/>; cantidades, precios e importes a 0.</item>
/// </list>
/// </summary>
public sealed class LineaFacturaVentaBorrador : BaseEntity
{
    public Guid FacturaVentaBorradorId { get; set; }

    /// <summary>Asignado por el sistema: máximo vigente del borrador + 10000 (10000 si no hay ninguna), como en los diarios.</summary>
    public int NumeroLinea { get; set; }

    public TipoLineaFactura Tipo { get; set; }

    public Guid? ProductoId { get; set; }
    public Guid? CuentaContableId { get; set; }

    public string? Descripcion { get; set; }

    public Guid? AlmacenId { get; set; }
    public Guid? UnidadMedidaId { get; set; }

    /// <summary>Factor congelado de <see cref="UnidadMedidaId"/> a la unidad base del producto (1 en CuentaContable, 0 en Comentario).</summary>
    public decimal CantidadPorUnidadMedida { get; set; }

    /// <summary>&gt; 0 salvo en Comentario (0).</summary>
    public decimal Cantidad { get; set; }

    public decimal PrecioUnitario { get; set; }
    public decimal PorcentajeDescuentoLinea { get; set; }

    /// <summary><c>ROUND(Cantidad × PrecioUnitario × PorcentajeDescuentoLinea / 100, 2)</c>.</summary>
    public decimal ImporteDescuentoLinea { get; set; }

    /// <summary><c>ROUND(Cantidad × PrecioUnitario, 2) − ImporteDescuentoLinea</c> (neto del descuento).</summary>
    public decimal ImporteLinea { get; set; }

    public Guid? GrupoProductoId { get; set; }
    public Guid? GrupoIvaProductoId { get; set; }
    public Guid? GrupoInventarioId { get; set; }

    public string? IdentificadorIva { get; set; }
    public decimal PorcentajeIva { get; set; }
}
