namespace OpenSource1.Core.Enums;

/// <summary>
/// Las tres tablas de intersección contable (setups, Fase 5, Task 5.4): <see cref="General"/> (grupo de negocio × grupo de
/// producto), <see cref="Iva"/> (grupo de IVA de negocio × grupo de IVA de producto) e <see cref="Inventario"/> (almacén ×
/// grupo de inventario). Cada una es su propia tabla y su propio mantenimiento bajo <c>api/setups-contables/{tipo}</c>.
/// </summary>
public enum TipoSetupContable : short
{
    General = 1,
    Iva = 2,
    Inventario = 3
}
