using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.Contabilidad;
using OpenSource1.Application.Features.Contabilidad.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Infrastructure.Data.Queries;

/// <summary>
/// Consultas del libro contable: listados paginados (Task 5.5; filtros de tipo/número de documento, socio y registro en la 7.4)
/// y balance de comprobación (Task 7.4). Orden por allow-list (<see cref="ColumnasPermitidas"/>) con desempate estable por
/// <c>Id</c>. Sin filtro de borrado lógico: el libro no lo tiene, y el nombre de la cuenta (o del socio) se muestra aunque esté
/// borrada (el movimiento es historia). Solo lectura.
/// </summary>
public sealed class DapperContabilidadReadRepository(IDbSession session) : IContabilidadReadRepository
{
    private static readonly ColumnasPermitidas ColumnasMovimientos = new("Id", "FechaRegistro", "NumeroCuenta", "Importe", "NumeroDocumento");
    private static readonly ColumnasPermitidas ColumnasRegistros = new("Id", "NumeroRegistro", "FechaCreacion");

    public async Task<Result<PagedResult<MovimientoContableResponse>>> ListMovimientosAsync(
        MovimientoContableSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);
        var pagina = paginacion.Normalizar();
        var filtros = new List<string>();
        var parameters = new DynamicParameters();

        if (search.CuentaContableId is { } cuentaId)
        {
            filtros.Add("""m."CuentaContableId" = @CuentaContableId""");
            parameters.Add("CuentaContableId", cuentaId);
        }

        if (search.Desde is { } desde)
        {
            filtros.Add("""m."FechaRegistro" >= @Desde""");
            parameters.Add("Desde", desde);
        }

        if (search.Hasta is { } hasta)
        {
            filtros.Add("""m."FechaRegistro" <= @Hasta""");
            parameters.Add("Hasta", hasta);
        }

        if (search.TipoDocumento is { } tipo)
        {
            filtros.Add("""m."TipoDocumento" = @TipoDocumento""");
            parameters.Add("TipoDocumento", (short)tipo);
        }

        if (search.SocioNegocioId is { } socioId)
        {
            filtros.Add("""m."SocioNegocioId" = @SocioNegocioId""");
            parameters.Add("SocioNegocioId", socioId);
        }

        if (search.RegistroContableId is { } registroId)
        {
            filtros.Add("""m."RegistroContableId" = @RegistroContableId""");
            parameters.Add("RegistroContableId", registroId);
        }

        // Número de documento por contenido (ILIKE con los metacaracteres escapados; mismo criterio que las vistas de inventario).
        // Sin prefijo de alias: el WHERE solo se aplica sobre "MovimientosContables" (conteo y CTE de la página), sin joins.
        FilterExpressionBuilder.AddTextFilter(filtros, parameters, ColumnasMovimientos, "NumeroDocumento", search.NumeroDocumento);

        var whereSql = filtros.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", filtros);

        // Por defecto, orden cronológico (FechaRegistro, Id): el orden natural de un mayor por cuenta.
        var ordenColumna = ColumnasMovimientos.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "FechaRegistro";
        var direccion = pagina.Descendente ? "DESC" : "ASC";

        parameters.Add("TamanoPagina", pagina.TamanoPagina);
        parameters.Add("Offset", pagina.Offset);

        var countSql = $"""SELECT COUNT(*) FROM "MovimientosContables" m {whereSql}""";
        // La página se corta sobre el libro solo (todas las columnas de orden y filtro son suyas) y después se unen los maestros
        // a esas filas: con un OFFSET grande no se resuelven los joins de todo lo que se salta (Task 7.4, 100 000 movimientos).
        var orden = $"""m.{ColumnasMovimientos.Citar(ordenColumna)} {direccion}, m."Id" {direccion}""";
        var pageSql = $"""
            WITH pagina AS (
                SELECT m.* FROM "MovimientosContables" m
                {whereSql}
                ORDER BY {orden}
                LIMIT @TamanoPagina OFFSET @Offset
            )
            SELECT m."Id", m."CuentaContableId", m."NumeroCuenta", c."Nombre" AS "NombreCuenta", m."FechaRegistro",
                   m."FechaDocumento", m."TipoDocumento", m."NumeroDocumento", m."Descripcion", m."Importe", m."Debito",
                   m."Credito", m."RegistroContableId", r."NumeroRegistro", m."SocioNegocioId", s."Codigo" AS "SocioCodigo",
                   s."NombreComercial" AS "SocioNombre", m."ProductoId",
                   m."GrupoNegocioId", m."GrupoProductoId", m."GrupoIvaNegocioId", m."GrupoIvaProductoId", m."TipoOrigen",
                   m."ClaveOrigen", m."CreatedAtUtc", m."CreatedBy"
            FROM pagina m
            JOIN "RegistrosContables" r ON r."Id" = m."RegistroContableId"
            LEFT JOIN "CuentasContables" c ON c."Id" = m."CuentaContableId"
            LEFT JOIN "SociosNegocio" s ON s."Id" = m."SocioNegocioId"
            ORDER BY {orden}
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var total = await session.Connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));
        var items = await session.Connection.QueryAsync<MovimientoContableResponse>(
            new CommandDefinition(pageSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        return Result<PagedResult<MovimientoContableResponse>>.Exito(
            new PagedResult<MovimientoContableResponse>(items.AsList(), pagina.Pagina, pagina.TamanoPagina, total));
    }

    public async Task<Result<PagedResult<RegistroContableResponse>>> ListRegistrosAsync(
        PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        var pagina = paginacion.Normalizar();
        var parameters = new DynamicParameters();
        parameters.Add("TamanoPagina", pagina.TamanoPagina);
        parameters.Add("Offset", pagina.Offset);

        var ordenColumna = ColumnasRegistros.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "Id";
        var direccion = pagina.Descendente ? "DESC" : "ASC";

        // Totales derivados del libro (índice IX_MovimientosContables_RegistroContableId), solo para la página pedida.
        var pageSql = $"""
            SELECT r."Id", r."NumeroRegistro", r."DesdeMovimiento", r."HastaMovimiento", t."Movimientos", t."TotalDebito",
                   t."TotalCredito", r."TipoOrigen", r."ClaveOrigen", r."FechaCreacion", r."CreadoPor"
            FROM (
                SELECT * FROM "RegistrosContables"
                ORDER BY {ColumnasRegistros.Citar(ordenColumna)} {direccion}, "Id" {direccion}
                LIMIT @TamanoPagina OFFSET @Offset
            ) r
            CROSS JOIN LATERAL (
                SELECT COUNT(*)::int AS "Movimientos", COALESCE(SUM(m."Debito"), 0) AS "TotalDebito",
                       COALESCE(SUM(m."Credito"), 0) AS "TotalCredito"
                FROM "MovimientosContables" m WHERE m."RegistroContableId" = r."Id"
            ) t
            ORDER BY r.{ColumnasRegistros.Citar(ordenColumna)} {direccion}, r."Id" {direccion}
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var total = await session.Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """SELECT COUNT(*) FROM "RegistrosContables" """, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));
        var items = await session.Connection.QueryAsync<RegistroContableResponse>(
            new CommandDefinition(pageSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        return Result<PagedResult<RegistroContableResponse>>.Exito(
            new PagedResult<RegistroContableResponse>(items.AsList(), pagina.Pagina, pagina.TamanoPagina, total));
    }

    /// <summary>
    /// Balance de comprobación (Task 7.4, desviación F7): una pasada por el libro hasta <c>hasta</c> agrupada por cuenta, con
    /// agregados filtrados: saldo inicial = Σ Importe antes de <c>desde</c>; débitos y créditos = Σ Debito / Σ Credito del rango;
    /// saldo final = Σ Importe hasta <c>hasta</c> (calculado aparte, no como suma de las otras columnas). Sin cierre de ejercicio:
    /// las cuentas de resultado acumulan igual que las de balance. Se listan las cuentas con movimientos en el rango o saldo
    /// inicial ≠ 0 (el libro solo admite cuentas de Posteo) y, como títulos sin importes, las cuentas de Encabezado no borradas;
    /// todo por número (<c>COLLATE "C"</c>: orden por caracteres, "1" &lt; "1101" &lt; "2") y después por Id.
    /// Las cuentas de totalización (Total, InicioTotal, FinTotal) no se muestran: no hay reglas de totalización en el plan.
    /// </summary>
    public async Task<BalanceComprobacionResponse> GetBalanceComprobacionAsync(
        BalanceComprobacionCriterios criterios, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criterios);
        var parameters = new DynamicParameters();
        var enRango = "TRUE";
        var antesDelRango = "FALSE";
        if (criterios.Desde is { } desde)
        {
            enRango = """m."FechaRegistro" >= @Desde""";
            antesDelRango = """m."FechaRegistro" < @Desde""";
            parameters.Add("Desde", desde);
        }

        var hastaSql = string.Empty;
        if (criterios.Hasta is { } hasta)
        {
            hastaSql = """WHERE m."FechaRegistro" <= @Hasta""";
            parameters.Add("Hasta", hasta);
        }

        var sql = $"""
            WITH saldos AS (
                SELECT m."CuentaContableId",
                       COALESCE(SUM(m."Importe") FILTER (WHERE {antesDelRango}), 0) AS "SaldoInicial",
                       COALESCE(SUM(m."Debito") FILTER (WHERE {enRango}), 0) AS "Debitos",
                       COALESCE(SUM(m."Credito") FILTER (WHERE {enRango}), 0) AS "Creditos",
                       SUM(m."Importe") AS "SaldoFinal",
                       (COUNT(*) FILTER (WHERE {enRango}))::int AS "Movimientos"
                FROM "MovimientosContables" m
                {hastaSql}
                GROUP BY m."CuentaContableId"
            )
            SELECT f.* FROM (
                SELECT c."Id" AS "CuentaContableId", c."Numero", c."Nombre", c."TipoCuenta", c."TipoResultado", c."Sangria",
                       FALSE AS "EsEncabezado", c."IsDeleted" AS "Borrada", s."SaldoInicial", s."Debitos", s."Creditos",
                       s."SaldoFinal", s."Movimientos"
                FROM saldos s
                JOIN "CuentasContables" c ON c."Id" = s."CuentaContableId"
                WHERE s."Movimientos" > 0 OR s."SaldoInicial" <> 0
                UNION ALL
                SELECT c."Id", c."Numero", c."Nombre", c."TipoCuenta", c."TipoResultado", c."Sangria", TRUE, FALSE,
                       NULL, NULL, NULL, NULL, NULL
                FROM "CuentasContables" c
                WHERE c."TipoCuenta" = {(short)TipoCuentaContable.Encabezado} AND NOT c."IsDeleted"
            ) f
            ORDER BY f."Numero" COLLATE "C", f."EsEncabezado" DESC, f."CuentaContableId"
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var filas = (await session.Connection.QueryAsync<BalanceComprobacionFilaResponse>(
            new CommandDefinition(sql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken))).AsList();

        var posteo = filas.Where(f => !f.EsEncabezado).ToList();
        var totales = new BalanceComprobacionTotales(
            posteo.Sum(f => f.SaldoInicial ?? 0m), posteo.Sum(f => f.Debitos ?? 0m), posteo.Sum(f => f.Creditos ?? 0m),
            posteo.Sum(f => f.SaldoFinal ?? 0m));
        return new BalanceComprobacionResponse(criterios.Desde, criterios.Hasta, filas, totales);
    }
}
