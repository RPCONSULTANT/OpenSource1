using MediatR;
using OpenSource1.Application.Features.SetupsContables.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.SetupsContables.Commands;

// Convenio común de los tres setups (Task 5.4):
// - El eje principal (GrupoProductoId / GrupoIvaProductoId / GrupoInventarioId) es obligatorio.
// - El eje secundario (GrupoNegocioId / GrupoIvaNegocioId / AlmacenId) null = COMODÍN ("cualquiera"); Guid.Empty también se
//   lee como comodín. Como null ya significa comodín, en la modificación los dos ejes son de REEMPLAZO COMPLETO (no hay
//   "null = conservar" en las claves).
// - Toda cuenta que se asigna o CAMBIA debe ser de Posteo y no estar bloqueada (400 setup_contable.cuenta_invalida); una ya
//   asignada que no cambia no se revalida (mismo criterio que los grupos de cliente: la rechaza el derivador al postear).
// - Combinación ya existente entre las filas vivas → 409 setup_contable.conflicto. Xmin obligatorio en el PUT.

public sealed record CreateSetupGeneralCommand(
    Guid? GrupoNegocioId,
    Guid GrupoProductoId,
    Guid CuentaVentasId,
    Guid CuentaCostoVentasId,
    Guid CuentaDescuentoVentasId,
    Guid CuentaAjusteInventarioId) : IRequest<Result<SetupGeneralResponse>>;

public sealed record UpdateSetupGeneralCommand(
    Guid Id,
    Guid? GrupoNegocioId,
    Guid GrupoProductoId,
    Guid CuentaVentasId,
    Guid CuentaCostoVentasId,
    Guid CuentaDescuentoVentasId,
    Guid CuentaAjusteInventarioId,
    long Xmin) : IRequest<Result<SetupGeneralResponse>>;

public sealed record CreateSetupIvaCommand(
    Guid? GrupoIvaNegocioId,
    Guid GrupoIvaProductoId,
    decimal PorcentajeIva,
    Guid CuentaIvaVentasId,
    Guid? CuentaIvaComprasId,
    string IdentificadorIva,
    TipoCalculoIva TipoCalculoIva) : IRequest<Result<SetupIvaResponse>>;

/// <summary>
/// <c>CuentaIvaComprasId</c> es la única cuenta opcional: <see langword="null"/> = conservar, <see cref="Guid.Empty"/> = quitarla
/// (mismo convenio que las cuentas opcionales del grupo de cliente). El resto es de reemplazo completo.
/// </summary>
public sealed record UpdateSetupIvaCommand(
    Guid Id,
    Guid? GrupoIvaNegocioId,
    Guid GrupoIvaProductoId,
    decimal PorcentajeIva,
    Guid CuentaIvaVentasId,
    Guid? CuentaIvaComprasId,
    string IdentificadorIva,
    TipoCalculoIva TipoCalculoIva,
    long Xmin) : IRequest<Result<SetupIvaResponse>>;

public sealed record CreateSetupInventarioCommand(
    Guid? AlmacenId,
    Guid GrupoInventarioId,
    Guid CuentaInventarioId,
    Guid CuentaAjusteInventarioId,
    Guid CuentaVariacionCostoId) : IRequest<Result<SetupInventarioResponse>>;

public sealed record UpdateSetupInventarioCommand(
    Guid Id,
    Guid? AlmacenId,
    Guid GrupoInventarioId,
    Guid CuentaInventarioId,
    Guid CuentaAjusteInventarioId,
    Guid CuentaVariacionCostoId,
    long Xmin) : IRequest<Result<SetupInventarioResponse>>;

/// <summary>Borrado lógico de una fila de cualquiera de los tres setups. Un setup no lo referencia nadie: no hay guarda de uso.</summary>
public sealed record DeleteSetupContableCommand(TipoSetupContable Tipo, Guid Id) : IRequest<Result>;
