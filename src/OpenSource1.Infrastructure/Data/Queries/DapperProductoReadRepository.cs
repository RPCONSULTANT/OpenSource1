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
    // producto no desaparece del listado si su categoría o su unidad fue borrada lógicamente. Existencia NO está aquí: ver
    // ProductosAplanados/ExistenciaLateralSql más abajo.
    private const string ProductosAplanadosSinExistencia = """
        SELECT p."Id", p."Codigo", p."Nombre", p."PrecioVenta",
               p."CategoriaId", c."Codigo" AS "CategoriaCodigo", c."Nombre" AS "CategoriaNombre",
               p."UnidadMedidaBaseId", u."Codigo" AS "UnidadMedidaCodigo", u."Nombre" AS "UnidadMedidaNombre",
               COALESCE(u."Decimales", 0) AS "UnidadMedidaBaseDecimales",
               p."MetodoCosteo", p."CostoUnitario", p."CostoEstandar", p."CostoAjustado", p."Bloqueado", p."ImagePath",
               p."CreatedAtUtc", p."UpdatedAtUtc", p."CreatedBy", p."UpdatedBy"
        FROM "Productos" p
        LEFT JOIN "CategoriasProducto" c ON c."Id" = p."CategoriaId"
        LEFT JOIN "UnidadesMedida" u ON u."Id" = p."UnidadMedidaBaseId"
        WHERE p."IsDeleted" = false
        """;

    // Existencia (Task 3.6, reemplaza a la columna Stock): suma de MovimientosProducto.Cantidad por producto, en TODOS los
    // almacenes y sin corte de fecha (a hoy).
    //
    // CORRECCIÓN 2 (ronda 1, IMPORTANT 2): hay DOS formas de calcularla, elegidas según si hace falta ANTES de paginar
    // (para filtrar por "existencia"/"stockState" u ordenar por "Existencia") o no:
    //  - EAGER (esta constante, <see cref="ProductosAplanados"/>): agrega TODA la tabla de movimientos una sola vez y la
    //    deja pegada a cada producto candidato. Obligatoria cuando el resultado depende de la existencia de TODOS los
    //    productos candidatos antes de aplicar LIMIT/OFFSET (filtrar/contar por existencia, u ordenar por ella: no hay
    //    forma de ordenar por un valor derivado sin calcularlo para todas las filas candidatas primero). Con 30 000
    //    productos / 100 000 movimientos mide ~90 ms de punta a punta (EXPLAIN ANALYZE en el informe de la Task 3.6).
    //  - LAZY (<see cref="ExistenciaLateralSql"/>, un LEFT JOIN LATERAL): calcula la existencia SOLO para las filas que
    //    ya sobrevivieron el filtro/orden/LIMIT/OFFSET (la página, o la única fila de GetByIdAsync), como una subconsulta
    //    correlacionada por "ProductoId" que usa el índice no parcial IX_MovimientosProducto_Existencia
    //    ("ProductoId","AlmacenId") INCLUDE ("Cantidad","FechaRegistro") en vez de recorrer el libro entero. La usan
    //    GetByIdAsync (siempre: una fila) y ListAsync cuando ni el filtro ni el orden dependen de "Existencia".
    private const string ProductosAplanados = $"""
        SELECT b.*, COALESCE(e."Existencia", 0) AS "Existencia" FROM (
        {ProductosAplanadosSinExistencia}
        ) b
        LEFT JOIN (SELECT "ProductoId", SUM("Cantidad") AS "Existencia" FROM "MovimientosProducto" GROUP BY "ProductoId") e
            ON e."ProductoId" = b."Id"
        """;

    // Se aplica DESPUÉS de un WHERE/ORDER BY/LIMIT ya resuelto (alias "x" de esa subconsulta): correlaciona por
    // "ProductoId" fila a fila, así que Postgres solo agrega los movimientos de los productos que quedaron en el
    // resultado final, no los de toda la tabla.
    private const string ExistenciaLateralSql = """
        LEFT JOIN LATERAL (
            SELECT SUM(m."Cantidad") AS "Existencia" FROM "MovimientosProducto" m WHERE m."ProductoId" = x."Id"
        ) e ON true
        """;

    public async Task<ProductoResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Lazy siempre: filtra por Id PRIMERO (una fila) y solo entonces calcula su existencia; nunca agrega los
        // movimientos de los otros 29 999 productos para devolver uno solo.
        const string sql = $"""
            SELECT x.*, COALESCE(e."Existencia", 0) AS "Existencia" FROM (
                SELECT * FROM (
                {ProductosAplanadosSinExistencia}
                ) b
                WHERE b."Id" = @Id
            ) x
            {ExistenciaLateralSql}
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
        var tieneStockState = search.StockState is "with" or "without";
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

        // CORRECCIÓN 2: solo se paga el agregado EAGER (toda la tabla de movimientos) cuando el WHERE o el ORDER BY
        // dependen realmente de "Existencia"; si no, count y página usan la ruta LAZY (índice, por producto).
        var necesitaAgregadoEager = !string.IsNullOrWhiteSpace(search.Existencia) || tieneStockState
            || string.Equals(ordenColumna, "Existencia", StringComparison.Ordinal);

        var baseSqlConteo = necesitaAgregadoEager ? ProductosAplanados : ProductosAplanadosSinExistencia;
        var countSql = $"""
            SELECT COUNT(*) FROM (
            {baseSqlConteo}
            ) x
            {whereSql}
            """;

        // CORRECCIÓN 2: en la ruta EAGER, el orden/filtro por "Existencia" obliga a agregar TODA la tabla de movimientos,
        // pero NO a arrastrar las columnas anchas (ImagePath, auditoría...) durante el ORDER BY/LIMIT: eso encarecía
        // igual el Sort aunque solo se devuelvan @TamanoPagina filas. Por eso "ganadores" solo proyecta "Id" (Postgres
        // poda el resto antes de ordenar) y el JOIN de columnas anchas + la existencia final se hacen SOLO para esas
        // filas ya acotadas por LIMIT/OFFSET. Con 30 000/100 000 esto baja el peor caso medido (orden Y filtro por
        // Existencia en una página intermedia) de ~130 ms a ~85 ms (detalle en el informe de la Task 3.6).
        var pageSql = necesitaAgregadoEager
            ? $"""
                SELECT w.*, COALESCE(e."Existencia", 0) AS "Existencia" FROM (
                    SELECT * FROM (
                    {ProductosAplanadosSinExistencia}
                    ) b
                    WHERE b."Id" IN (
                        SELECT "Id" FROM (
                        {ProductosAplanados}
                        ) x
                        {whereSql}
                        ORDER BY {ordenSql} {direccionSql}, "Id" ASC
                        LIMIT @TamanoPagina OFFSET @Offset
                    )
                ) w
                LEFT JOIN LATERAL (
                    SELECT SUM(m."Cantidad") AS "Existencia" FROM "MovimientosProducto" m WHERE m."ProductoId" = w."Id"
                ) e ON true
                ORDER BY {ordenSql} {direccionSql}, "Id" ASC
                """
            : $"""
                SELECT x.*, COALESCE(e."Existencia", 0) AS "Existencia" FROM (
                    SELECT * FROM (
                    {ProductosAplanadosSinExistencia}
                    ) b
                    {whereSql}
                    ORDER BY {ordenSql} {direccionSql}, "Id" ASC
                    LIMIT @TamanoPagina OFFSET @Offset
                ) x
                {ExistenciaLateralSql}
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
