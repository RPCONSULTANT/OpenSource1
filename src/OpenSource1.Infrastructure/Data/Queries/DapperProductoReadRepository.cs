using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.Productos;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperProductoReadRepository(IDbSession session) : IProductoReadRepository
{
    private static readonly ColumnasPermitidas ColumnasPermitidas = new(
        "Codigo", "Nombre", "CategoriaCodigo", "CategoriaNombre", "UnidadMedidaCodigo", "UnidadMedidaNombre", "Precio", "Stock", "CreatedAtUtc");

    public async Task<ProductoResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id", "Codigo", "Nombre", "Precio", "Stock", "CategoriaCodigo", "CategoriaNombre", "UnidadMedidaCodigo", "UnidadMedidaNombre", "ImagePath", "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"
            FROM "Productos"
            WHERE "Id" = @Id AND "IsDeleted" = false
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

        var precioResult = FilterExpressionBuilder.AddExactFilter(filters, parameters, ColumnasPermitidas, "Precio", search.Precio,
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

        filters.Insert(0, "\"IsDeleted\" = false");
        var whereSql = Environment.NewLine + "WHERE " + string.Join(" AND ", filters);

        var ordenColumna = ColumnasPermitidas.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "CreatedAtUtc";
        var ordenSql = ColumnasPermitidas.Citar(ordenColumna);
        var direccionSql = pagina.Descendente ? "DESC" : "ASC";

        parameters.Add("TamanoPagina", pagina.TamanoPagina);
        parameters.Add("Offset", pagina.Offset);

        var countSql = $"""
            SELECT COUNT(*) FROM "Productos"
            {whereSql}
            """;

        var pageSql = $"""
            SELECT "Id", "Codigo", "Nombre", "Precio", "Stock", "CategoriaCodigo", "CategoriaNombre", "UnidadMedidaCodigo", "UnidadMedidaNombre", "ImagePath", "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"
            FROM "Productos"
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
