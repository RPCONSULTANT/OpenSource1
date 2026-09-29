namespace OpenSource1.Core.Entities.Contabilidad;

/// <summary>
/// Intersección grupo de negocio × grupo de producto (spec 5.3, <c>SetupsContableGeneral</c>): de la MISMA fila salen las cuentas
/// de ventas, costo de ventas, descuentos sobre ventas y ajuste de inventario. <see cref="GrupoProductoId"/> es el eje
/// principal (obligatorio); <see cref="GrupoNegocioId"/> <see langword="null"/> es el comodín ("cualquier grupo de negocio").
/// Única por (<see cref="GrupoNegocioId"/>, <see cref="GrupoProductoId"/>) entre las filas vivas, con <c>NULLS NOT DISTINCT</c>.
/// </summary>
public sealed class SetupContableGeneral : BaseEntity
{
    public Guid? GrupoNegocioId { get; set; }

    public Guid GrupoProductoId { get; set; }

    public Guid CuentaVentasId { get; set; }

    public Guid CuentaCostoVentasId { get; set; }

    public Guid CuentaDescuentoVentasId { get; set; }

    public Guid CuentaAjusteInventarioId { get; set; }
}

/// <summary>
/// Ids fijos de las filas semilla de los setups contables (Task 5.4), <c>f3000000-0000-0000-0000-0000000000NN</c>.
/// </summary>
public static class SetupContableIds
{
    public static readonly Guid GeneralNacionalBienes = Guid.Parse("f3000000-0000-0000-0000-000000000001");
    public static readonly Guid GeneralNacionalServicios = Guid.Parse("f3000000-0000-0000-0000-000000000002");
    public static readonly Guid GeneralCualquieraBienes = Guid.Parse("f3000000-0000-0000-0000-000000000003");
    public static readonly Guid GeneralCualquieraServicios = Guid.Parse("f3000000-0000-0000-0000-000000000004");
    public static readonly Guid IvaItbis18Itbis18 = Guid.Parse("f3000000-0000-0000-0000-000000000005");
    public static readonly Guid IvaItbis18Exento = Guid.Parse("f3000000-0000-0000-0000-000000000006");
    public static readonly Guid IvaExentoItbis18 = Guid.Parse("f3000000-0000-0000-0000-000000000007");
    public static readonly Guid IvaExentoExento = Guid.Parse("f3000000-0000-0000-0000-000000000008");
    public static readonly Guid InventarioCualquieraGeneral = Guid.Parse("f3000000-0000-0000-0000-000000000009");
}
