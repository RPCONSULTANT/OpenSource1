using OpenSource1.Core.Common;

namespace OpenSource1.Application.Services.Registro;

/// <summary>
/// Validación de la fecha de registro de un posteo contra las fechas permitidas (Task 8.5): si el usuario actual
/// (<c>IUsuarioActual.Id</c>) tiene una excepción propia manda SOLO su rango; si no (o si es un proceso del sistema, sin Id),
/// el rango general. Un fallo es <c>registro.fecha_no_permitida</c> con el rango vigente y su origen en el mensaje y
/// <c>Campo = "FechaRegistro"</c> (el llamador lo adapta, p. ej. <c>Lineas[n].FechaRegistro</c>).
/// </summary>
/// <remarks>
/// Lo consumen los comandos de posteo (diarios, facturas, notas de crédito, cobros y aplicaciones) ANTES de escribir nada,
/// dentro de su transacción; no los procesos del sistema (batch de costo, ajuste de costo) ni el alta/edición de borradores.
/// El rango vigente se lee una sola vez por instancia (scoped: una petición/un posteo) aunque se validen varias fechas.
/// </remarks>
public interface IValidadorFechaRegistro
{
    Task<Result> ValidarAsync(DateOnly fecha, CancellationToken cancellationToken = default);
}
