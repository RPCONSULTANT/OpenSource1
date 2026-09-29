using OpenSource1.Core.Common;

namespace OpenSource1.Application.Services.Inventario;

/// <summary>Resultado de <see cref="IAjusteCostoInventario.AjustarAsync"/>.</summary>
/// <param name="ProductosAjustados">Productos que quedaron con <c>CostoAjustado = true</c> (cada uno en su propia transacción); no cuenta los que
/// siguen pendientes por una salida sin costo determinable (sus movimientos insertados sí cuentan en el otro campo).</param>
/// <param name="MovimientosValorCreados">Filas de <c>MovimientosValor</c> insertadas (ajustes de costo + redondeos).</param>
public sealed record ResultadoAjusteCosto(int ProductosAjustados, int MovimientosValorCreados);

/// <summary>
/// Rutina idempotente "Ajustar costo movimientos" (Task 3.5): recalcula el costo promedio móvil diario de cada producto
/// en orden cronológico e inserta movimientos de valor de ajuste/redondeo (append-only: nunca actualiza ni borra filas
/// del libro).
/// </summary>
public interface IAjusteCostoInventario
{
    /// <summary>
    /// Productos con <c>CostoAjustado = false</c> (incluidos los borrados lógicamente: su historia cuenta), o solo
    /// <paramref name="productoId"/> si se indica (aunque ya esté ajustado). Una transacción por producto. Requiere que NO
    /// haya transacción activa en la sesión (<c>inventario.ajuste_en_transaccion</c>): cada producto confirma la suya.
    /// </summary>
    Task<Result<ResultadoAjusteCosto>> AjustarAsync(Guid? productoId, CancellationToken ct = default);
}
