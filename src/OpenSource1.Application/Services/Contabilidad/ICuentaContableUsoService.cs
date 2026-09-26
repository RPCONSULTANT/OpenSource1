namespace OpenSource1.Application.Services.Contabilidad;

/// <summary>
/// Guarda de uso de una cuenta contable: borrarla, o cambiar su <c>TipoCuenta</c> de Posteo a otro
/// valor, se rechaza con 409 <c>cuenta_contable.conflicto</c> si tiene movimientos contables o está
/// referenciada por un setup contable o un grupo de cliente. Cubre los grupos de cliente (Task 5.3),
/// los setups contables (Task 5.4) no borrados y los movimientos del libro contable (Task 5.5), sin tocar los llamadores
/// (<c>DeleteCuentaContableCommandHandler</c>, <c>UpdateCuentaContableCommandHandler</c>).
/// </summary>
public interface ICuentaContableUsoService
{
    Task<bool> EstaEnUsoAsync(Guid cuentaContableId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bloquea la fila de la cuenta (<c>FOR UPDATE</c>, borrada o no) hasta el commit/rollback. Requiere transacción activa.
    /// Se toma ANTES de <see cref="EstaEnUsoAsync"/> al borrar la cuenta o sacarla de Posteo: <c>IRegistroContable</c> lee
    /// las cuentas <c>FOR SHARE</c>, así que un registro en curso hace esperar al borrado (que después ve sus movimientos) o
    /// espera al borrado (y después ve la cuenta borrada y lo rechaza). Nunca queda una cuenta borrada con movimientos.
    /// </summary>
    Task BloquearAsync(Guid cuentaContableId, CancellationToken cancellationToken = default);
}
