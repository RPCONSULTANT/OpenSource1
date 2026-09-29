using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Services.Contabilidad;

/// <summary>Resultado de <see cref="IDerivadorCuentas.IvaAsync"/>: la fila de <c>SetupsIva</c> aplicable.</summary>
public sealed record SetupIvaResuelto(
    decimal PorcentajeIva,
    Guid CuentaIvaVentasId,
    Guid? CuentaIvaComprasId,
    string IdentificadorIva,
    TipoCalculoIva TipoCalculo);

/// <summary>
/// Derivador de cuentas (spec 5.4, D4: ninguna cuenta se captura en un documento; se deriva de sus clasificadores al postear).
/// Solo LEE (Dapper sobre la sesión del scope, así ve la transacción del llamador) y nunca escribe.
/// <list type="bullet">
/// <item>El grupo principal de cada lookup (grupo de producto en el general, de IVA de producto en el de IVA, de inventario en el
/// de inventario, grupo contable de cliente en CxC) es obligatorio: <see langword="null"/> → <c>setup_contable.grupo_faltante</c>.</item>
/// <item>Resolución: fila exacta (ambas claves) → fila con el eje secundario <c>NULL</c> (comodín) → <c>setup_contable.inexistente</c>
/// con los CÓDIGOS de la combinación. Solo filas no borradas; el comodín solo existe en el eje secundario.</item>
/// <item>La cuenta resuelta debe existir, ser de Posteo y no estar bloqueada → si no, <c>setup_contable.cuenta_invalida</c>.</item>
/// </list>
/// </summary>
public interface IDerivadorCuentas
{
    Task<Result<Guid>> CuentaCxCAsync(Guid? grupoClienteContableId, CancellationToken ct = default);

    Task<Result<Guid>> CuentaVentasAsync(Guid? grupoNegocioId, Guid? grupoProductoId, CancellationToken ct = default);

    Task<Result<Guid>> CuentaCostoVentasAsync(Guid? grupoNegocioId, Guid? grupoProductoId, CancellationToken ct = default);

    Task<Result<Guid>> CuentaDescuentoVentasAsync(Guid? grupoNegocioId, Guid? grupoProductoId, CancellationToken ct = default);

    /// <summary>Solo se valida la cuenta de IVA de VENTAS (la de compras es opcional y la usará el módulo de compras).</summary>
    Task<Result<SetupIvaResuelto>> IvaAsync(Guid? grupoIvaNegocioId, Guid? grupoIvaProductoId, CancellationToken ct = default);

    Task<Result<Guid>> CuentaInventarioAsync(Guid? almacenId, Guid? grupoInventarioId, CancellationToken ct = default);

    Task<Result<Guid>> CuentaAjusteInventarioAsync(Guid? almacenId, Guid? grupoInventarioId, CancellationToken ct = default);
}
