namespace OpenSource1.Application.Features.DiariosInventario.Lotes;

/// <summary>
/// Bloqueo transaccional (<c>SELECT ... FOR UPDATE</c>) de la fila de un lote de diario. Serializa altas de línea
/// concurrentes del mismo lote (Ronda de corrección 1 de la Task 4.2): sin este lock, dos altas simultáneas pueden
/// calcular el mismo <c>NumeroLinea</c> (23505 confuso en vez de un reintento limpio) o saltarse el tope de 1000
/// líneas leyendo el mismo conteo antes de que la otra inserte. La Task 4.3 reutiliza el mismo lock para serializar
/// el posteo de un lote contra altas/bajas de línea concurrentes.
/// </summary>
public interface ILoteDiarioBloqueoService
{
    /// <summary>
    /// Requiere una transacción activa (el lock se libera al confirmarla o deshacerla, igual que
    /// <c>IGeneradorNumeroDocumento</c>). Devuelve <see langword="null"/> si el lote no existe (o está borrado
    /// lógicamente); si existe, su <c>Bloqueado</c> vigente, con la fila ya bloqueada para el resto de la
    /// transacción actual.
    /// </summary>
    Task<bool?> BloquearYObtenerEstadoAsync(Guid loteDiarioId, CancellationToken cancellationToken = default);
}
