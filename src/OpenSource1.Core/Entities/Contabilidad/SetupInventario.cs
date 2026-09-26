namespace OpenSource1.Core.Entities.Contabilidad;

/// <summary>
/// Intersección almacén × grupo de inventario (spec 5.3, <c>SetupsInventario</c>): cuenta de activo del inventario, de ajuste
/// y de variación de costo. <see cref="GrupoInventarioId"/> es el eje principal (obligatorio); <see cref="AlmacenId"/>
/// <see langword="null"/> es el comodín ("cualquier almacén").
/// </summary>
public sealed class SetupInventario : BaseEntity
{
    public Guid? AlmacenId { get; set; }

    public Guid GrupoInventarioId { get; set; }

    public Guid CuentaInventarioId { get; set; }

    public Guid CuentaAjusteInventarioId { get; set; }

    public Guid CuentaVariacionCostoId { get; set; }
}
