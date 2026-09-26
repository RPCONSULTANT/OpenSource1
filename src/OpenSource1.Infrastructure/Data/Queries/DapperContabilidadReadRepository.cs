using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.Contabilidad;
using OpenSource1.Application.Features.Contabilidad.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

/// <summary>
/// Listados paginados del libro contable (Task 5.5). Orden por allow-list (<see cref="ColumnasPermitidas"/>) con desempate
/// estable por <c>Id</c>. Sin filtro de borrado lógico: el libro no lo tiene, y el nombre de la cuenta se muestra aunque la
/// cuenta esté borrada (el movimiento es historia).
/// </summary>
public sealed class DapperContabilidadReadRepository(IDbSession session) : IContabilidadReadRepository
{
    private static readonly ColumnasPermitidas ColumnasMovimientos = new("Id", "FechaRegistro", "NumeroCuenta", "Importe");
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

        var whereSql = filtros.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", filtros);

        // Por defecto, orden cronológico (FechaRegistro, Id): el orden natural de un mayor por cuenta.
        var ordenColumna = ColumnasMovimientos.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "FechaRegistro";
        var direccion = pagina.Descendente ? "DESC" : "ASC";

        parameters.Add("TamanoPagina", pagina.TamanoPagina);
        parameters.Add("Offset", pagina.Offset);

        var countSql = $"""SELECT COUNT(*) FROM "MovimientosContables" m {whereSql}""";
        var pageSql = $"""
            SELECT m."Id", m."CuentaContableId", m."NumeroCuenta", c."Nombre" AS "NombreCuenta", m."FechaRegistro",
                   m."FechaDocumento", m."TipoDocumento", m."NumeroDocumento", m."Descripcion", m."Importe", m."Debito",
                   m."Credito", m."RegistroContableId", r."NumeroRegistro", m."SocioNegocioId", m."ProductoId",
                   m."GrupoNegocioId", m."GrupoProductoId", m."GrupoIvaNegocioId", m."GrupoIvaProductoId", m."TipoOrigen",
                   m."ClaveOrigen", m."CreatedAtUtc", m."CreatedBy"
            FROM "MovimientosContables" m
            JOIN "RegistrosContables" r ON r."Id" = m."RegistroContableId"
            LEFT JOIN "CuentasContables" c ON c."Id" = m."CuentaContableId"
            {whereSql}
            ORDER BY m.{ColumnasMovimientos.Citar(ordenColumna)} {direccion}, m."Id" {direccion}
            LIMIT @TamanoPagina OFFSET @Offset
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
}
