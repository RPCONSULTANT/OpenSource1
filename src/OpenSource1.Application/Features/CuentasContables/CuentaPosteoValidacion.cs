using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.CuentasContables;

/// <summary>
/// Regla común de los maestros que referencian cuentas (grupos contables de cliente, Task 5.3; setups contables, Task 5.4): la
/// cuenta existe (no borrada), es de <see cref="TipoCuentaContable.Posteo"/> y no está bloqueada; si no, 400
/// <c>&lt;prefijo&gt;.cuenta_invalida</c> en el campo indicado (nunca <c>.no_encontrado</c>: es un dato del cuerpo, no el recurso de
/// la URL). Devuelve la cuenta para rotular la respuesta.
/// </summary>
internal static class CuentaPosteoValidacion
{
    public static async Task<Result<CuentaContable>> ValidarAsync(
        IUnitOfWork unitOfWork, Guid cuentaId, string prefijo, string campo, string etiqueta, CancellationToken cancellationToken)
    {
        var cuenta = await unitOfWork.Repository<CuentaContable>()
            .FirstOrDefaultAsync(x => x.Id == cuentaId, cancellationToken: cancellationToken);

        if (cuenta is null || cuenta.TipoCuenta != TipoCuentaContable.Posteo || cuenta.Bloqueada)
        {
            var motivo = cuenta is null ? "no existe"
                : cuenta.Bloqueada ? $"({cuenta.Numero}) está bloqueada"
                : $"({cuenta.Numero}) no es de posteo";
            return Result<CuentaContable>.Fallo(new Error(
                $"{prefijo}.cuenta_invalida",
                $"La cuenta {etiqueta} {motivo}: debe ser una cuenta de posteo no bloqueada.",
                campo));
        }

        return Result<CuentaContable>.Exito(cuenta);
    }
}
