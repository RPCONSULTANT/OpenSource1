using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.Productos;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperProductoReadRepository(IDbSession session) : IProductoReadRepository
{
    private static readonly ColumnasPermitidas ColumnasPermitidas = new(
        "Codigo", "Nombre", "CategoriaCodigo", "CategoriaNombre", "UnidadMedidaCodigo", "UnidadMedidaNombre", "PrecioVenta", "Existencia", "CreatedAtUtc");

    // ColumnasPermitidas.Citar genera nombres de columna SIN alias de tabla ("Codigo", "Nombre", "Id", "CreatedAtUtc"...), y esas
    // columnas existen a la vez en Productos, CategoriasProducto y UnidadesMedida: filtrar u ordenar directamente sobre el JOIN daría
    // "column reference is ambiguous". Por eso el JOIN va envuelto en una subconsulta que aplana las columnas con alias ÚNICOS
    // (CategoriaCodigo, UnidadMedidaNombre...) y todo filtro, orden, recuento y paginación se aplica sobre ese resultado. LEFT JOIN: un
    // producto no desaparece del listado si su categoría o su unidad fue borrada lógicamente.
    //
    // Existencia (Task 3.6, reemplaza a la columna Stock): suma de MovimientosProducto.Cantidad por producto, en TODOS los
    // almacenes y sin corte de fecha (a hoy). Va en un LEFT JOIN a una subconsulta agregada (no una subconsulta correlacionada
    // por fila) para que Postgres la resuelva una sola vez; COALESCE a 0 para los productos sin movimientos todavía.
    private const string ProductosAplanados = """
        SELECT p."Id", p."Codigo", p."Nombre", p."PrecioVenta",
               p."CategoriaId", c."Codigo" AS "CategoriaCodigo", c."Nombre" AS "CategoriaNombre",
               p."UnidadMedidaBaseId", u."Codigo" AS "UnidadMedidaCodigo", u."Nombre" AS "UnidadMedidaNombre",
               COALESCE(u."Decimales", 0) AS "UnidadMedidaBaseDecimales",
               p."MetodoCosteo", p."CostoUnitario", p."CostoEstandar", p."CostoAjustado", p."Bloqueado", p."ImagePath",
               p."CreatedAtUtc", p."UpdatedAtUtc", p."CreatedBy", p."UpdatedBy",
               COALESCE(e."Existencia", 0) AS "Existencia"
        FROM "Productos" p
        LEFT JOIN "CategoriasProducto" c ON c."Id" = p."CategoriaId"
        LEFT JOIN "UnidadesMedida" u ON u."Id" = p."UnidadMedidaBaseId"
        LEFT JOIN (SELECT "ProductoId", SUM("Cantidad") AS "Existencia" FROM "MovimientosProducto" GROUP BY "ProductoId") e
            ON e."ProductoId" = p."Id"
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

        var existenciaResult = FilterExpressionBuilder.AddMinimumFilter(filters, parameters, ColumnasPermitidas, "Existencia", search.Existencia,
            static term => (decimal.TryParse(term, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value), value));
        if (existenciaResult.EsFallo)
        {
            return Result<PagedResult<ProductoResponse>>.Fallo(existenciaResult);
        }

        // StockState: "all" (o vacío/desconocido) no filtra; "with" = existencia > 0; "without" = existencia <= 0. Condición fija
        // (sin entrada de usuario en el SQL), así que no pasa por ColumnasPermitidas ni por parámetros.
        switch (search.StockState)
        {
            case "with":
                filters.Add("\"Existencia\" > 0");
                break;
            case "without":
                filters.Add("\"Existencia\" <= 0");
                break;
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
