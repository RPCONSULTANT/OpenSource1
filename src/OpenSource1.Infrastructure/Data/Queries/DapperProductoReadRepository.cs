using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.Productos;
using OpenSource1.Application.Features.Productos.Dtos;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperProductoReadRepository(IDbConnectionFactory connectionFactory) : IProductoReadRepository
{
    public async Task<ProductoResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id", "Codigo", "Nombre", "Precio", "Stock", "CategoriaCodigo", "CategoriaNombre", "UnidadMedidaCodigo", "UnidadMedidaNombre", "ImagePath", "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"
            FROM "Productos"
            WHERE "Id" = @Id
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ProductoResponse>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ProductoResponse>> ListAsync(ProductoSearchCriteria search, CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT "Id", "Codigo", "Nombre", "Precio", "Stock", "CategoriaCodigo", "CategoriaNombre", "UnidadMedidaCodigo", "UnidadMedidaNombre", "ImagePath", "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"
            FROM "Productos"
            """;

        var filters = new List<string>();
        var parameters = new DynamicParameters();

        FilterExpressionBuilder.AddTextFilter(filters, parameters, "Codigo", search.Codigo);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, "Nombre", search.Nombre);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, "CategoriaCodigo", search.CategoriaCodigo);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, "CategoriaNombre", search.CategoriaNombre);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, "UnidadMedidaCodigo", search.UnidadMedidaCodigo);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, "UnidadMedidaNombre", search.UnidadMedidaNombre);
        FilterExpressionBuilder.AddExactFilter(filters, parameters, "Precio", search.Precio,
            static term => (decimal.TryParse(term, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value), value));
        FilterExpressionBuilder.AddExactFilter(filters, parameters, "Stock", search.Stock,
            static term => (int.TryParse(term, out var value), value));

        if (filters.Count > 0)
        {
            sql += Environment.NewLine + "WHERE " + string.Join(" AND ", filters);
        }

        sql += Environment.NewLine + "ORDER BY \"CreatedAtUtc\" DESC";

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var result = await connection.QueryAsync<ProductoResponse>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }
}
