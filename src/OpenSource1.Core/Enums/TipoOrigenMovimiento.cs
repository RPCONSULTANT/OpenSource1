namespace OpenSource1.Core.Enums;

/// <summary>
/// Subsistema que generó el movimiento (para trazabilidad e idempotencia vía <c>ClaveOrigen</c>).
/// Se guarda como <c>smallint</c>. <see cref="Migracion"/> = 99 la usa la migración de datos de la
/// Task 3.6 para el Stock legado.
/// </summary>
public enum TipoOrigenMovimiento : short
{
    Diario = 1,
    FacturaVenta = 2,
    AjusteCosto = 3,
    Migracion = 99
}
