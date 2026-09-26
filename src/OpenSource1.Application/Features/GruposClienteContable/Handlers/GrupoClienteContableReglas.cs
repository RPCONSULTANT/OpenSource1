using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.CuentasContables;
using OpenSource1.Application.Features.GruposClienteContable.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Contabilidad;

namespace OpenSource1.Application.Features.GruposClienteContable.Handlers;

/// <summary>Reglas compartidas por Create/Update de <see cref="GrupoClienteContable"/>.</summary>
internal static class GrupoClienteContableReglas
{
    public const string Prefijo = "grupo_cliente_contable";

    /// <summary>Ver <see cref="CuentaPosteoValidacion"/>: 400 <c>grupo_cliente_contable.cuenta_invalida</c> en el campo indicado.</summary>
    public static Task<Result<CuentaContable>> ValidarCuentaAsync(
        IUnitOfWork unitOfWork, Guid cuentaId, string campo, string etiqueta, CancellationToken cancellationToken) =>
        CuentaPosteoValidacion.ValidarAsync(unitOfWork, cuentaId, Prefijo, campo, etiqueta, cancellationToken);

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
