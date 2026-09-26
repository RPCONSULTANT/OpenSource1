using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Infrastructure.Services.Contabilidad;

/// <summary>
/// Implementación Dapper de <see cref="IDerivadorCuentas"/> sobre la sesión del scope (<see cref="IDbSession"/>): cada consulta
/// se ejecuta con <c>session.CurrentTransaction</c>, así que dentro de un posteo ve también las filas aún no confirmadas de la
/// transacción del llamador. Nunca escribe.
/// <para>
/// Resolución en UNA consulta: las filas vivas con el eje principal igual al pedido y el secundario igual al pedido O
/// <c>NULL</c>, ordenadas con la exacta primero (<c>ORDER BY secundario IS NULL</c>: <see langword="false"/> antes que
/// <see langword="true"/>), <c>LIMIT 1</c>. Con el secundario pedido <see langword="null"/>, <c>= NULL</c> no casa nada y solo
/// queda el comodín. El comodín existe solo en el eje secundario (el principal es NOT NULL en las tres tablas), así que un
/// comodín de un eje nunca se confunde con el otro.
/// </para>
/// Tablas y columnas interpoladas salen de las constantes de esta clase, nunca de datos del usuario.
/// </summary>
public sealed class DerivadorCuentas(IDbSession session) : IDerivadorCuentas
{
    private sealed record Setup(
        string Tabla,
        string ColumnaSecundaria, string EtiquetaSecundaria, string TablaSecundaria,
        string ColumnaPrincipal, string EtiquetaPrincipal, string TablaPrincipal, string NombrePrincipal);

    private static readonly Setup General = new(
        "SetupsContableGeneral",
        "GrupoNegocioId", "GrupoNegocio", "GruposNegocio",
        "GrupoProductoId", "GrupoProducto", "GruposProducto", "grupo de producto");

    private static readonly Setup Iva = new(
        "SetupsIva",
        "GrupoIvaNegocioId", "GrupoIvaNegocio", "GruposIvaNegocio",
        "GrupoIvaProductoId", "GrupoIvaProducto", "GruposIvaProducto", "grupo de IVA de producto");

    private static readonly Setup Inventario = new(
        "SetupsInventario",
        "AlmacenId", "Almacen", "Almacenes",
        "GrupoInventarioId", "GrupoInventario", "GruposInventario", "grupo de inventario");

    public Task<Result<Guid>> CuentaVentasAsync(Guid? grupoNegocioId, Guid? grupoProductoId, CancellationToken ct = default) =>
        CuentaAsync(General, "CuentaVentasId", "de ventas", grupoNegocioId, grupoProductoId, ct);

    public Task<Result<Guid>> CuentaCostoVentasAsync(Guid? grupoNegocioId, Guid? grupoProductoId, CancellationToken ct = default) =>
        CuentaAsync(General, "CuentaCostoVentasId", "de costo de ventas", grupoNegocioId, grupoProductoId, ct);

    public Task<Result<Guid>> CuentaDescuentoVentasAsync(Guid? grupoNegocioId, Guid? grupoProductoId, CancellationToken ct = default) =>
        CuentaAsync(General, "CuentaDescuentoVentasId", "de descuentos sobre ventas", grupoNegocioId, grupoProductoId, ct);

    public Task<Result<Guid>> CuentaInventarioAsync(Guid? almacenId, Guid? grupoInventarioId, CancellationToken ct = default) =>
        CuentaAsync(Inventario, "CuentaInventarioId", "de inventario", almacenId, grupoInventarioId, ct);

    public Task<Result<Guid>> CuentaAjusteInventarioAsync(Guid? almacenId, Guid? grupoInventarioId, CancellationToken ct = default) =>
        CuentaAsync(Inventario, "CuentaAjusteInventarioId", "de ajuste de inventario", almacenId, grupoInventarioId, ct);

    public async Task<Result<SetupIvaResuelto>> IvaAsync(Guid? grupoIvaNegocioId, Guid? grupoIvaProductoId, CancellationToken ct = default)
    {
        const string extra = """, s."PorcentajeIva", s."CuentaIvaComprasId", s."IdentificadorIva", s."TipoCalculoIva" """;
        var fila = await ResolverAsync(Iva, "CuentaIvaVentasId", "de IVA en ventas", grupoIvaNegocioId, grupoIvaProductoId, extra, ct);
        if (!fila.TryObtenerValor(out var f))
        {
            return Result<SetupIvaResuelto>.Fallo(fila);
        }

        return Result<SetupIvaResuelto>.Exito(new SetupIvaResuelto(
            f.PorcentajeIva ?? 0m, f.CuentaId, f.CuentaIvaComprasId, f.IdentificadorIva ?? string.Empty,
            (TipoCalculoIva)(f.TipoCalculoIva ?? (short)TipoCalculoIva.Normal)));
    }

    public async Task<Result<Guid>> CuentaCxCAsync(Guid? grupoClienteContableId, CancellationToken ct = default)
    {
        if (grupoClienteContableId is not { } grupoId)
        {
            return Result<Guid>.Fallo(GrupoFaltante("grupo contable de cliente", "GrupoClienteContableId", "la cuenta por cobrar"));
        }

        const string sql = """
            SELECT g."CuentaCxCId" AS "CuentaId", c."Numero", c."TipoCuenta", c."Bloqueada", c."IsDeleted" AS "CuentaBorrada"
            FROM "GruposClienteContable" g
            LEFT JOIN "CuentasContables" c ON c."Id" = g."CuentaCxCId"
            WHERE g."Id" = @Id AND g."IsDeleted" = false
            """;

        await session.EnsureOpenAsync(ct);
        var fila = await session.Connection.QuerySingleOrDefaultAsync<Fila>(
            new CommandDefinition(sql, new { Id = grupoId }, session.CurrentTransaction, cancellationToken: ct));

        if (fila is not null && CuentaValida(fila))
        {
            return Result<Guid>.Exito(fila.CuentaId);
        }

        var codigo = await CodigoAsync("GruposClienteContable", grupoId, ct);
        return Result<Guid>.Fallo(fila is null
            ? new Error("setup_contable.inexistente", $"No existe setup GruposClienteContable para GrupoClienteContable={codigo}")
            : ValidarCuenta(fila, "por cobrar", $"del grupo contable de cliente {codigo}")!.Value);
    }

    private async Task<Result<Guid>> CuentaAsync(
        Setup setup, string columnaCuenta, string etiquetaCuenta, Guid? secundario, Guid? principal, CancellationToken ct)
    {
        var fila = await ResolverAsync(setup, columnaCuenta, etiquetaCuenta, secundario, principal, string.Empty, ct);
        return fila.TryObtenerValor(out var f) ? Result<Guid>.Exito(f.CuentaId) : Result<Guid>.Fallo(fila);
    }

    private async Task<Result<Fila>> ResolverAsync(
        Setup setup, string columnaCuenta, string etiquetaCuenta, Guid? secundario, Guid? principal, string columnasExtra, CancellationToken ct)
    {
        if (principal is not { } principalId)
        {
            return Result<Fila>.Fallo(GrupoFaltante(setup.NombrePrincipal, setup.ColumnaPrincipal, $"la cuenta {etiquetaCuenta}"));
        }

        var sql = $"""
            SELECT s."{columnaCuenta}" AS "CuentaId", c."Numero", c."TipoCuenta", c."Bloqueada", c."IsDeleted" AS "CuentaBorrada"{columnasExtra}
            FROM "{setup.Tabla}" s
            LEFT JOIN "CuentasContables" c ON c."Id" = s."{columnaCuenta}"
            WHERE s."IsDeleted" = false
              AND s."{setup.ColumnaPrincipal}" = @Principal
              AND (s."{setup.ColumnaSecundaria}" = @Secundario OR s."{setup.ColumnaSecundaria}" IS NULL)
            ORDER BY (s."{setup.ColumnaSecundaria}" IS NULL)
            LIMIT 1
            """;

        await session.EnsureOpenAsync(ct);
        var fila = await session.Connection.QuerySingleOrDefaultAsync<Fila>(
            new CommandDefinition(sql, new { Principal = principalId, Secundario = secundario }, session.CurrentTransaction, cancellationToken: ct));

        var combinacion = fila is null || !CuentaValida(fila) ? await CombinacionAsync(setup, secundario, principalId, ct) : null;
        if (fila is null)
        {
            return Result<Fila>.Fallo(new Error("setup_contable.inexistente", $"No existe setup {setup.Tabla} para {combinacion}"));
        }

        return ValidarCuenta(fila, etiquetaCuenta, $"del setup {setup.Tabla} para {combinacion}") is { } error
            ? Result<Fila>.Fallo(error)
            : Result<Fila>.Exito(fila);
    }

    private static bool CuentaValida(Fila fila) =>
        fila.CuentaBorrada == false && fila.TipoCuenta == (short)TipoCuentaContable.Posteo && fila.Bloqueada == false;

    private static Error? ValidarCuenta(Fila fila, string etiquetaCuenta, string origen)
    {
        if (CuentaValida(fila))
        {
            return null;
        }

        var motivo = fila.CuentaBorrada != false ? "no existe"
            : fila.Bloqueada == true ? $"({fila.Numero}) está bloqueada"
            : $"({fila.Numero}) no es de posteo";
        return new Error(
            "setup_contable.cuenta_invalida",
            $"La cuenta {etiquetaCuenta} {origen} {motivo}: debe ser una cuenta de posteo no bloqueada.");
    }

    private static Error GrupoFaltante(string nombreGrupo, string campo, string que) => new(
        "setup_contable.grupo_faltante",
        $"Falta el {nombreGrupo}: es obligatorio para derivar {que}.",
        campo);

    /// <summary>
    /// <c>GrupoA=código × GrupoB=código|cualquiera</c> (formato literal del plan): primero el eje principal, que siempre tiene
    /// código, y después el secundario, con <c>cualquiera</c> cuando se pidió sin él.
    /// </summary>
    private async Task<string> CombinacionAsync(Setup setup, Guid? secundario, Guid principal, CancellationToken ct)
    {
        var codigoPrincipal = await CodigoAsync(setup.TablaPrincipal, principal, ct);
        var codigoSecundario = secundario is { } s ? await CodigoAsync(setup.TablaSecundaria, s, ct) : "cualquiera";
        return $"{setup.EtiquetaPrincipal}={codigoPrincipal} × {setup.EtiquetaSecundaria}={codigoSecundario}";
    }

    /// <summary>Código del grupo/almacén (aunque esté borrado lógicamente: sigue identificándolo); "desconocido" si no existe.</summary>
    private async Task<string> CodigoAsync(string tabla, Guid id, CancellationToken ct)
    {
        await session.EnsureOpenAsync(ct);
        var codigo = await session.Connection.ExecuteScalarAsync<string?>(
            new CommandDefinition($"""SELECT "Codigo" FROM "{tabla}" WHERE "Id" = @Id""", new { Id = id }, session.CurrentTransaction, cancellationToken: ct));
        return codigo ?? "desconocido";
    }

    private sealed class Fila
    {
        public Guid CuentaId { get; init; }
        public string? Numero { get; init; }
        public short? TipoCuenta { get; init; }
        public bool? Bloqueada { get; init; }
        public bool? CuentaBorrada { get; init; }
        public decimal? PorcentajeIva { get; init; }
        public Guid? CuentaIvaComprasId { get; init; }
        public string? IdentificadorIva { get; init; }
        public short? TipoCalculoIva { get; init; }
    }
}
