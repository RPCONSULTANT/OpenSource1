namespace OpenSource1.Application.Services.Contabilidad;

/// <summary>Movimiento de valor que el batch de costo no pudo contabilizar en esta ejecución (sigue pendiente).</summary>
/// <param name="MovimientoValorId">Id de la fila de <c>MovimientosValor</c>.</param>
/// <param name="Codigo">Código del error (p. ej. <c>setup_contable.inexistente</c>, <c>setup_contable.grupo_faltante</c>).</param>
/// <param name="Mensaje">Mensaje con el detalle (combinación de setup que falta, cuenta inválida...).</param>
public sealed record PendientePosteoCosto(long MovimientoValorId, string Codigo, string Mensaje);

/// <summary>Resultado de una ejecución del batch <c>PostearCostoInventarioContabilidad</c> (Task 5.6).</summary>
/// <param name="Asientos">Registros contables creados (uno por producto y fecha con importe neto por cuenta distinto de 0).</param>
/// <param name="MovimientosValorContabilizados">Filas de <c>MovimientosValor</c> cuyo delta quedó contabilizado (incluidas las
/// de un grupo cuyo efecto neto es 0, p. ej. una transferencia entre almacenes con la misma cuenta de inventario).</param>
/// <param name="Pendientes">Movimientos que siguen pendientes, con el motivo.</param>
public sealed record ResultadoPosteoCostoInventario(
    int Asientos, int MovimientosValorContabilizados, IReadOnlyList<PendientePosteoCosto> Pendientes);

/// <summary>
/// Batch idempotente "Postear costo de inventario a contabilidad" (spec 5.6 y desviaciones de la Fase 5): contabiliza en el
/// libro contable el delta <c>ImporteCosto − ImporteCostoPosteadoContabilidad</c> de cada movimiento de valor pendiente y
/// después iguala <c>ImporteCostoPosteadoContabilidad</c> a <c>ImporteCosto</c> (la única columna actualizable del libro de
/// valor). Una segunda ejecución no encuentra delta y no escribe nada.
/// </summary>
public interface IPosteoCostoInventario
{
    /// <summary>
    /// Todos los movimientos pendientes, o solo los de <paramref name="productoId"/>. Abre y confirma UNA transacción por
    /// (producto, fecha de registro): un fallo de setup de un producto no bloquea a los demás. Lanza
    /// <see cref="InvalidOperationException"/> si la sesión ya tiene una transacción activa (garantiza su propia atomicidad,
    /// mismo criterio que <c>PostearLoteDiario</c>).
    /// </summary>
    Task<ResultadoPosteoCostoInventario> PostearAsync(Guid? productoId, CancellationToken ct = default);
}
