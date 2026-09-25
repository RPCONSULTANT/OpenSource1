using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.Productos;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperProductoReadRepository(IDbSession session) : IProductoReadRepository
{
    private static readonly ColumnasPermitidas ColumnasPermitidas = new(
        "Codigo", "Nombre", "CategoriaCodigo", "CategoriaNombre", "UnidadMedidaCodigo", "UnidadMedidaNombre", "PrecioVenta", "Stock", "CreatedAtUtc");

    // ColumnasPermitidas.Citar genera nombres de columna SIN alias de tabla ("Codigo", "Nombre", "Id", "CreatedAtUtc"...), y esas
    // columnas existen a la vez en Productos, CategoriasProducto y UnidadesMedida: filtrar u ordenar directamente sobre el JOIN daría
    // "column reference is ambiguous". Por eso el JOIN va envuelto en una subconsulta que aplana las columnas con alias ÚNICOS
    // (CategoriaCodigo, UnidadMedidaNombre...) y todo filtro, orden, recuento y paginación se aplica sobre ese resultado. LEFT JOIN: un
    // producto no desaparece del listado si su categoría o su unidad fue borrada lógicamente.
    private const string ProductosAplanados = """
        SELECT p."Id", p."Codigo", p."Nombre", p."PrecioVenta", p."Stock",
               p."CategoriaId", c."Codigo" AS "CategoriaCodigo", c."Nombre" AS "CategoriaNombre",
               p."UnidadMedidaBaseId", u."Codigo" AS "UnidadMedidaCodigo", u."Nombre" AS "UnidadMedidaNombre",
               p."MetodoCosteo", p."CostoUnitario", p."CostoEstandar", p."CostoAjustado", p."Bloqueado", p."ImagePath",
               p."CreatedAtUtc", p."UpdatedAtUtc", p."CreatedBy", p."UpdatedBy"
        FROM "Productos" p
        LEFT JOIN "CategoriasProducto" c ON c."Id" = p."CategoriaId"
        LEFT JOIN "UnidadesMedida" u ON u."Id" = p."UnidadMedidaBaseId"
        WHERE p."IsDeleted" = false
        """;

    public async Task<ProductoResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT * FROM (
            {ProductosAplanados}
            ) x
            WHERE x."Id" = @Id
            """;
        await session.EnsureOpenAsync(cancellationToken);
        return await session.Connection.QuerySingleOrDefaultAsync<ProductoResponse>(new CommandDefinition(sql, new { Id = id }, session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    public async Task<Result<PagedResult<ProductoResponse>>> ListAsync(
        ProductoSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        var pagina = paginacion.Normalizar();

        var filters = new List<string>();
        var parameters = new DynamicParameters();

        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Codigo", search.Codigo);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Nombre", search.Nombre);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "CategoriaCodigo", search.CategoriaCodigo);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "CategoriaNombre", search.CategoriaNombre);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "UnidadMedidaCodigo", search.UnidadMedidaCodigo);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "UnidadMedidaNombre", search.UnidadMedidaNombre);

        var precioResult = FilterExpressionBuilder.AddExactFilter(filters, parameters, ColumnasPermitidas, "PrecioVenta", search.PrecioVenta,
            static term => (decimal.TryParse(term, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value), value));
        if (precioResult.EsFallo)
        {
            return Result<PagedResult<ProductoResponse>>.Fallo(precioResult);
        }

        var stockResult = FilterExpressionBuilder.AddExactFilter(filters, parameters, ColumnasPermitidas, "Stock", search.Stock,
            static term => (int.TryParse(term, out var value), value));
        if (stockResult.EsFallo)
        {
            return Result<PagedResult<ProductoResponse>>.Fallo(stockResult);
        }

        var whereSql = filters.Count == 0 ? string.Empty : Environment.NewLine + "WHERE " + string.Join(" AND ", filters);

        var ordenColumna = ColumnasPermitidas.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "CreatedAtUtc";
        var ordenSql = ColumnasPermitidas.Citar(ordenColumna);
        var direccionSql = pagina.Descendente ? "DESC" : "ASC";

        parameters.Add("TamanoPagina", pagina.TamanoPagina);
        parameters.Add("Offset", pagina.Offset);

        var countSql = $"""
            SELECT COUNT(*) FROM (
            {ProductosAplanados}
            ) x
            {whereSql}
            """;

        var pageSql = $"""
            SELECT * FROM (
            {ProductosAplanados}
            ) x
            {whereSql}
            ORDER BY {ordenSql} {direccionSql}, "Id" ASC
            LIMIT @TamanoPagina OFFSET @Offset
            """;

        await session.EnsureOpenAsync(cancellationToken);

        var total = await session.Connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        var items = await session.Connection.QueryAsync<ProductoResponse>(
            new CommandDefinition(pageSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        return Result<PagedResult<ProductoResponse>>.Exito(
            new PagedResult<ProductoResponse>(items.AsList(), pagina.Pagina, pagina.TamanoPagina, total));
    }
}
