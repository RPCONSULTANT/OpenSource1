namespace OpenSource1.Application.Services.Contabilidad;

/// <summary>
/// Guarda de uso de una cuenta contable: borrarla, o cambiar su <c>TipoCuenta</c> de Posteo a otro
/// valor, se rechaza con 409 <c>cuenta_contable.conflicto</c> si tiene movimientos contables o está
/// referenciada por un setup contable o un grupo de cliente. Cubre los grupos de cliente (Task 5.3)
/// y los setups contables (Task 5.4) no borrados; la Task 5.5 amplía <see cref="EstaEnUsoAsync"/> con los movimientos, sin tocar los llamadores
/// (<c>DeleteCuentaContableCommandHandler</c>, <c>UpdateCuentaContableCommandHandler</c>).
/// </summary>
public interface ICuentaContableUsoService
{
    Task<bool> EstaEnUsoAsync(Guid cuentaContableId, CancellationToken cancellationToken = default);
}
