using OpenSource1.Core.Enums;

namespace OpenSource1.Core.Entities;

public sealed class Producto : BaseEntity
{
    public required string Codigo { get; set; }
    public required string Nombre { get; set; }

    /// <summary>Precio de venta (antes <c>Precio</c>). <c>numeric(18,4)</c>.</summary>
    public decimal PrecioVenta { get; set; }

    // Stock (Task 3.6): eliminado. La existencia se deriva del libro de inventario (MovimientosProducto),
    // sumando "Cantidad" por producto/almacén/fecha (ver IConsultaInventario). El Stock legado se migró a
    // movimientos de apertura con TipoOrigen = Migracion en el almacén PRINCIPAL.

    /// <summary>Unidad de medida base del producto (catálogo <see cref="UnidadMedida"/>).</summary>
    public Guid UnidadMedidaBaseId { get; set; }

    public MetodoCosteo MetodoCosteo { get; set; } = MetodoCosteo.Promedio;

    /// <summary>
    /// Proyección del costo unitario: NO es autoritativa ni editable por el cliente HTTP; la mantiene la
    /// rutina de costeo de la Fase 3. Un producto nuevo nace con 0.
    /// </summary>
    public decimal CostoUnitario { get; set; }

    /// <summary>Costo estándar (informativo, editable).</summary>
    public decimal CostoEstandar { get; set; }

    /// <summary>
    /// Lo mantiene el sistema (rutina de costeo de la Fase 3), no el cliente HTTP. <c>true</c> = sin
    /// movimientos pendientes de ajustar (un producto nuevo no tiene ninguno).
    /// </summary>
    public bool CostoAjustado { get; set; } = true;

    /// <summary>Categoría del producto (catálogo <see cref="CategoriaProducto"/>).</summary>
    public Guid CategoriaId { get; set; }

    public BloqueoProducto Bloqueado { get; set; } = BloqueoProducto.Ninguno;
    public string? ImagePath { get; set; }
}
