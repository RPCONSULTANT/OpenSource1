using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.GruposClienteContable;
using OpenSource1.Application.Features.GruposClienteContable.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

/// <summary>
/// Lectura de los grupos contables de cliente con el número/nombre de sus cuentas. Mismo motivo que en
/// <see cref="DapperProductoReadRepository"/>: el JOIN va envuelto en una subconsulta que aplana las columnas con alias únicos,
/// y todo filtro/orden (nombres sin alias de tabla de <see cref="ColumnasPermitidas"/>) se aplica sobre ella. LEFT JOIN: el
/// grupo no desaparece si una de sus cuentas se borra lógicamente.
/// </summary>
public sealed class DapperGrupoClienteContableReadRepository(IDbSession session) : IGrupoClienteContableReadRepository
{
    private static readonly ColumnasPermitidas ColumnasPermitidas = new("Codigo", "Descripcion", "CreatedAtUtc");

    private const string GruposAplanados = """
        SELECT g."Id", g."Codigo", g."Descripcion",
               g."CuentaCxCId", COALESCE(cxc."Numero", '') AS "CuentaCxCNumero", COALESCE(cxc."Nombre", '') AS "CuentaCxCNombre",
               g."CuentaDescuentoId", d."Numero" AS "CuentaDescuentoNumero", d."Nombre" AS "CuentaDescuentoNombre",
               g."CuentaInteresId", i."Numero" AS "CuentaInteresNumero", i."Nombre" AS "CuentaInteresNombre",
               g.xmin::text::bigint AS "Xmin", g."CreatedAtUtc", g."UpdatedAtUtc", g."CreatedBy", g."UpdatedBy"
        FROM "GruposClienteContable" g
        LEFT JOIN "CuentasContables" cxc ON cxc."Id" = g."CuentaCxCId"
        LEFT JOIN "CuentasContables" d ON d."Id" = g."CuentaDescuentoId"
        LEFT JOIN "CuentasContables" i ON i."Id" = g."CuentaInteresId"
        WHERE g."IsDeleted" = false
        """;

    public async Task<GrupoClienteContableResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT * FROM (
            {GruposAplanados}
            ) x
            WHERE x."Id" = @Id
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, session.CurrentTransaction, cancellationToken: cancellationToken);
        return await session.Connection.QuerySingleOrDefaultAsync<GrupoClienteContableResponse>(command);
    }

    public async Task<Result<PagedResult<GrupoClienteContableResponse>>> ListAsync(
        GrupoClienteContableSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        var pagina = paginacion.Normalizar();

        var filters = new List<string>();
        var parameters = new DynamicParameters();

        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Codigo", search.Codigo);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Descripcion", search.Descripcion);

        var whereSql = filters.Count == 0 ? string.Empty : Environment.NewLine + "WHERE " + string.Join(" AND ", filters);

        var ordenColumna = ColumnasPermitidas.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "Codigo";
        var ordenSql = ColumnasPermitidas.Citar(ordenColumna);
        var direccionSql = pagina.Descendente ? "DESC" : "ASC";

        parameters.Add("TamanoPagina", pagina.TamanoPagina);
        parameters.Add("Offset", pagina.Offset);

        var countSql = $"""
            SELECT COUNT(*) FROM (
            {GruposAplanados}
            ) x
            {whereSql}
            """;

        var pageSql = $"""
            SELECT * FROM (
            {GruposAplanados}
            ) x
            {whereSql}
            ORDER BY {ordenSql} {direccionSql}, "Id" ASC
            LIMIT @TamanoPagina OFFSET @Offset
            """;

        await session.EnsureOpenAsync(cancellationToken);

        var total = await session.Connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        var items = await session.Connection.QueryAsync<GrupoClienteContableResponse>(
            new CommandDefinition(pageSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        return Result<PagedResult<GrupoClienteContableResponse>>.Exito(
            new PagedResult<GrupoClienteContableResponse>(items.AsList(), pagina.Pagina, pagina.TamanoPagina, total));
    }
}
