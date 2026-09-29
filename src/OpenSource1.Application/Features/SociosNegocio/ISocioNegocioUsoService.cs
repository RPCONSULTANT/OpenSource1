namespace OpenSource1.Application.Features.SociosNegocio;

/// <summary>
/// Guarda de uso de un socio de negocio (pendiente de la Fase 5, Task 6.4): borrarlo se rechaza con 409
/// <c>socio_negocio.conflicto</c> si es vender-a o facturar-a de un borrador de factura vivo, o si tiene facturas posteadas,
/// movimientos de cliente, movimientos contables o movimientos de inventario. Mismo patrón que <c>ICuentaContableUsoService</c>.
/// </summary>
public interface ISocioNegocioUsoService
{
    Task<bool> EstaEnUsoAsync(Guid socioNegocioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bloquea la fila del socio (<c>FOR UPDATE</c>, borrado o no) hasta el commit/rollback. Requiere transacción activa. Se toma
    /// ANTES de <see cref="EstaEnUsoAsync"/>: el posteo de una factura lee sus socios <c>FOR SHARE</c>, así que un posteo en curso
    /// hace esperar al borrado (que después ve la factura) o espera al borrado (y después ve el socio borrado y lo rechaza).
    /// </summary>
    Task BloquearAsync(Guid socioNegocioId, CancellationToken cancellationToken = default);
}
