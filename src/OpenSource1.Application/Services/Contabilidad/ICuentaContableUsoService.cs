namespace OpenSource1.Application.Services.Contabilidad;

/// <summary>
/// Guarda de uso de una cuenta contable: borrarla, o cambiar su <c>TipoCuenta</c> de Posteo a otro
/// valor, se rechaza con 409 <c>cuenta_contable.conflicto</c> si tiene movimientos contables o está
/// referenciada por un setup contable o un grupo de cliente. Desde la Task 5.3 cubre los grupos de
/// cliente (CxC, descuento o interés de un grupo no borrado); las Tasks 5.4-5.5 amplían
/// <see cref="EstaEnUsoAsync"/> con setups y movimientos, sin tocar los llamadores
/// (<c>DeleteCuentaContableCommandHandler</c>, <c>UpdateCuentaContableCommandHandler</c>).
/// </summary>
public interface ICuentaContableUsoService
{
    Task<bool> EstaEnUsoAsync(Guid cuentaContableId, CancellationToken cancellationToken = default);
}
