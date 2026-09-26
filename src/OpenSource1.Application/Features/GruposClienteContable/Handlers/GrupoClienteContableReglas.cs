using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.GruposClienteContable.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.GruposClienteContable.Handlers;

/// <summary>Reglas compartidas por Create/Update de <see cref="GrupoClienteContable"/>.</summary>
internal static class GrupoClienteContableReglas
{
    public const string Prefijo = "grupo_cliente_contable";

    /// <summary>
    /// La cuenta referenciada existe (no borrada), es de <see cref="TipoCuentaContable.Posteo"/> y no está bloqueada; si no,
    /// 400 <c>grupo_cliente_contable.cuenta_invalida</c> en el campo indicado (nunca <c>.no_encontrado</c>: es un dato del
    /// cuerpo, no el recurso de la URL). Devuelve la cuenta para rotular la respuesta.
    /// </summary>
    public static async Task<Result<CuentaContable>> ValidarCuentaAsync(
        IUnitOfWork unitOfWork, Guid cuentaId, string campo, string etiqueta, CancellationToken cancellationToken)
    {
        var cuenta = await unitOfWork.Repository<CuentaContable>()
            .FirstOrDefaultAsync(x => x.Id == cuentaId, cancellationToken: cancellationToken);

        if (cuenta is null || cuenta.TipoCuenta != TipoCuentaContable.Posteo || cuenta.Bloqueada)
        {
            var motivo = cuenta is null ? "no existe"
                : cuenta.Bloqueada ? $"({cuenta.Numero}) está bloqueada"
                : $"({cuenta.Numero}) no es de posteo";
            return Result<CuentaContable>.Fallo(new Error(
                $"{Prefijo}.cuenta_invalida",
                $"La cuenta {etiqueta} {motivo}: debe ser una cuenta de posteo no bloqueada.",
                campo));
        }

        return Result<CuentaContable>.Exito(cuenta);
    }

    public static Task<CuentaContable?> BuscarCuentaAsync(IUnitOfWork unitOfWork, Guid? id, CancellationToken cancellationToken) =>
        id is { } cuentaId
            ? unitOfWork.Repository<CuentaContable>().FirstOrDefaultAsync(x => x.Id == cuentaId, cancellationToken: cancellationToken)
            : Task.FromResult<CuentaContable?>(null);

    public static Error NoEncontrado() => new(
        $"{Prefijo}.no_encontrado", "No se encontró el grupo contable de cliente solicitado.", "Id");

    public static GrupoClienteContableResponse ToResponse(
        GrupoClienteContable x, CuentaContable? cxc, CuentaContable? descuento, CuentaContable? interes, long xmin) => new()
    {
        Id = x.Id,
        Codigo = x.Codigo,
        Descripcion = x.Descripcion,
        CuentaCxCId = x.CuentaCxCId,
        CuentaCxCNumero = cxc?.Numero ?? string.Empty,
        CuentaCxCNombre = cxc?.Nombre ?? string.Empty,
        CuentaDescuentoId = x.CuentaDescuentoId,
        CuentaDescuentoNumero = descuento?.Numero,
        CuentaDescuentoNombre = descuento?.Nombre,
        CuentaInteresId = x.CuentaInteresId,
        CuentaInteresNumero = interes?.Numero,
        CuentaInteresNombre = interes?.Nombre,
        Xmin = xmin,
        CreatedAtUtc = x.CreatedAtUtc.UtcDateTime,
        UpdatedAtUtc = x.UpdatedAtUtc?.UtcDateTime,
        CreatedBy = x.CreatedBy,
        UpdatedBy = x.UpdatedBy
    };
}
