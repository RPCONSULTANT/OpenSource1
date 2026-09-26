using MediatR;
using OpenSource1.Application.Features.GruposClienteContable.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.GruposClienteContable.Commands;

/// <summary>Alta de un grupo contable de cliente. <c>CuentaCxCId</c> es obligatoria; las otras dos cuentas son opcionales.</summary>
public sealed record CreateGrupoClienteContableCommand(
    string Codigo,
    string Descripcion,
    Guid CuentaCxCId,
    Guid? CuentaDescuentoId = null,
    Guid? CuentaInteresId = null) : IRequest<Result<GrupoClienteContableResponse>>;

/// <summary>
/// Modificación de un grupo contable de cliente. <c>Codigo</c>, <c>Descripcion</c> y <c>CuentaCxCId</c> son de reemplazo
/// completo. <c>CuentaDescuentoId</c>/<c>CuentaInteresId</c> son opcionales con semántica de MODIFICACIÓN PARCIAL:
/// <see langword="null"/> = conservar; <see cref="Guid.Empty"/> = quitar la cuenta; otro valor = se aplica (mismo convenio que
/// <c>TerminoPagoId</c> en SocioNegocio). Toda cuenta que CAMBIA debe ser de Posteo y no estar bloqueada (400
/// <c>grupo_cliente_contable.cuenta_invalida</c>); una ya asignada que no cambia no se revalida. <c>Xmin</c> es obligatorio.
/// </summary>
public sealed record UpdateGrupoClienteContableCommand(
    Guid Id,
    string Codigo,
    string Descripcion,
    Guid CuentaCxCId,
    Guid? CuentaDescuentoId,
    Guid? CuentaInteresId,
    long Xmin) : IRequest<Result<GrupoClienteContableResponse>>;

public sealed record DeleteGrupoClienteContableCommand(Guid Id) : IRequest<Result>;
