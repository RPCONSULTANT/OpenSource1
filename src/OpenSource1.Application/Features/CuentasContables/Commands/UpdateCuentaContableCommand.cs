using MediatR;
using OpenSource1.Application.Features.CuentasContables.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.CuentasContables.Commands;

/// <summary>
/// Modificación de una cuenta contable. <c>Numero</c>, <c>Nombre</c>, <c>TipoCuenta</c> y
/// <c>TipoResultado</c> son de reemplazo completo (siempre se envían, mismo patrón que
/// Codigo/Nombre en Almacen). <c>PosteoDirecto</c>, <c>Bloqueada</c> y <c>Sangria</c> tienen
/// semántica de MODIFICACIÓN PARCIAL: <see langword="null"/> (ausente) = conservar el valor actual.
/// <c>Xmin</c> es obligatorio: es el valor visto por el cliente al leer la cuenta (concurrencia
/// optimista vía <c>xmin</c> de Postgres); si no coincide con el vigente, la modificación falla con
/// 409 <c>entidad.modificada_por_otro</c>. Cambiar <c>TipoCuenta</c> de Posteo a otro valor mientras
/// la cuenta está en uso (<see cref="Services.Contabilidad.ICuentaContableUsoService"/>) falla con
/// 409 <c>cuenta_contable.conflicto</c>.
/// </summary>
public sealed record UpdateCuentaContableCommand(
    Guid Id,
    string Numero,
    string Nombre,
    TipoCuentaContable TipoCuenta,
    TipoResultadoCuenta TipoResultado,
    bool? PosteoDirecto,
    bool? Bloqueada,
    int? Sangria,
    long Xmin) : IRequest<Result<CuentaContableResponse>>;
