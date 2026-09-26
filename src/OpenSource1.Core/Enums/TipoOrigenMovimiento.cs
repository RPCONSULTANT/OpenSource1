namespace OpenSource1.Core.Enums;

/// <summary>
/// Subsistema que generó el movimiento (para trazabilidad e idempotencia vía <c>ClaveOrigen</c>).
/// Se guarda como <c>smallint</c>. <see cref="Migracion"/> = 99 la usa la migración de datos de la
/// Task 3.6 para el Stock legado. <see cref="CostoInventario"/> = 4 (Task 5.5) la usa el batch de contabilización del
/// costo de inventario (Task 5.6). Valores nuevos se añaden, nunca se renumeran.
/// </summary>
public enum TipoOrigenMovimiento : short
{
    Diario = 1,
    FacturaVenta = 2,
    AjusteCosto = 3,
    CostoInventario = 4,
    Migracion = 99
}
