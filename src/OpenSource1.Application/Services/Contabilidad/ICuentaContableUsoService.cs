namespace OpenSource1.Application.Services.Contabilidad;

/// <summary>
/// Guarda de uso de una cuenta contable: borrarla, o cambiar su <c>TipoCuenta</c> de Posteo a otro
/// valor, se rechaza con 409 <c>cuenta_contable.conflicto</c> si tiene movimientos contables o está
/// referenciada por un setup contable o un grupo de cliente. Ninguna de esas tablas existe todavía en
/// la Task 5.2 (nacen en las Tasks 5.3-5.5), así que la implementación de hoy siempre responde
/// <see langword="false"/>; cada task siguiente amplía <see cref="EstaEnUsoAsync"/> con su propia
/// consulta, sin tocar los llamadores (<c>DeleteCuentaContableCommandHandler</c>,
/// <c>UpdateCuentaContableCommandHandler</c>).
/// </summary>
public interface ICuentaContableUsoService
{
    Task<bool> EstaEnUsoAsync(Guid cuentaContableId, CancellationToken cancellationToken = default);
}
