namespace OpenSource1.Core.Entities;

/// <summary>
/// Equivalencia de una unidad de medida para un producto: cuántas unidades base del producto
/// contiene una unidad de <see cref="UnidadMedidaId"/> (p. ej. 1 CJA = 12 UND).
/// </summary>
public sealed class UnidadMedidaProducto : BaseEntity
{
    public Guid ProductoId { get; set; }
    public Guid UnidadMedidaId { get; set; }
    public decimal CantidadPorUnidadMedida { get; set; }
}
