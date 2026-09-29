using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.CategoriasProducto;
using OpenSource1.Application.Features.CategoriasProducto.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperCategoriaProductoReadRepository(IDbSession session) : ICategoriaProductoReadRepository
{
    private static readonly ColumnasPermitidas ColumnasPermitidas = new("Codigo", "Nombre", "CreatedAtUtc");

    // El nombre del padre sale de una subconsulta correlacionada (y no de un JOIN) para que las
    // columnas sin calificar de los filtros/orden ("Codigo", "Nombre") no sean ambiguas. El alias
    // "padre" es de la subconsulta; la tabla externa se referencia por su nombre completo.
    private const string ColumnasSelect = """
        "Id", "Codigo", "Nombre", "CategoriaPadreId",
        (SELECT padre."Nombre" FROM "CategoriasProducto" padre
          WHERE padre."Id" = "CategoriasProducto"."CategoriaPadreId" AND padre."IsDeleted" = false) AS "CategoriaPadreNombre",
        "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"
        """;

    public async Task<CategoriaProductoResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {ColumnasSelect}
            FROM "CategoriasProducto"
            WHERE "Id" = @Id AND "IsDeleted" = false
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, session.CurrentTransaction, cancellationToken: cancellationToken);
        return await session.Connection.QuerySingleOrDefaultAsync<CategoriaProductoResponse>(command);
    }

    public async Task<Result<PagedResult<CategoriaProductoResponse>>> ListAsync(
        CategoriaProductoSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        var pagina = paginacion.Normalizar();

        var filters = new List<string>();
        var parameters = new DynamicParameters();

        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Codigo", search.Codigo);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Nombre", search.Nombre);

        filters.Insert(0, "\"IsDeleted\" = false");
        var whereSql = Environment.NewLine + "WHERE " + string.Join(" AND ", filters);

        var ordenColumna = ColumnasPermitidas.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "CreatedAtUtc";
        var ordenSql = ColumnasPermitidas.Citar(ordenColumna);
        var direccionSql = pagina.Descendente ? "DESC" : "ASC";

        parameters.Add("TamanoPagina", pagina.TamanoPagina);
        parameters.Add("Offset", pagina.Offset);

        var countSql = $"""
            SELECT COUNT(*) FROM "CategoriasProducto"
            {whereSql}
            """;

        var pageSql = $"""
            SELECT {ColumnasSelect}
            FROM "CategoriasProducto"
            {whereSql}
            ORDER BY {ordenSql} {direccionSql}, "Id" ASC
            LIMIT @TamanoPagina OFFSET @Offset
            """;

        await session.EnsureOpenAsync(cancellationToken);

        var total = await session.Connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        var items = await session.Connection.QueryAsync<CategoriaProductoResponse>(
            new CommandDefinition(pageSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        return Result<PagedResult<CategoriaProductoResponse>>.Exito(
            new PagedResult<CategoriaProductoResponse>(items.AsList(), pagina.Pagina, pagina.TamanoPagina, total));
    }
}
