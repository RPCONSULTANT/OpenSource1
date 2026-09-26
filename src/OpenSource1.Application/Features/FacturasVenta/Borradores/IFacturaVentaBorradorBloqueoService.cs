using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.FacturasVenta.Borradores;

/// <summary>
/// Bloqueo transaccional (<c>SELECT ... FOR UPDATE</c>) de la fila de un borrador de factura, análogo a
/// <c>ILoteDiarioBloqueoService</c>: serializa el alta/modificación/borrado de líneas, la modificación de cabecera,
/// liberar/reabrir y el borrado del borrador (y el posteo de la Task 6.4). Sin él, dos altas concurrentes calcularían el
/// mismo <c>NumeroLinea</c>, o una línea podría colarse en un borrador que otra transacción está liberando o borrando.
/// </summary>
public interface IFacturaVentaBorradorBloqueoService
{
    /// <summary>
    /// Requiere una transacción activa (lanza <see cref="InvalidOperationException"/> si no la hay). Devuelve
    /// <see langword="null"/> si el borrador no existe (o está borrado lógicamente); si existe, su <c>Estado</c> vigente, con la
    /// fila ya bloqueada hasta el commit/rollback.
    /// </summary>
    Task<EstadoFacturaBorrador?> BloquearYObtenerEstadoAsync(Guid facturaVentaBorradorId, CancellationToken cancellationToken = default);
}
