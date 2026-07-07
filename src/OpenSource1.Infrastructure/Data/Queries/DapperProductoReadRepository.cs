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
            SELECT "Id", "Codigo", "Nombre", "Precio", "Stock", "Categoria", "ImagePath", "CreatedAtUtc", "UpdatedAtUtc"
            FROM "Productos"
            WHERE "Id" = @Id
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ProductoResponse>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ProductoResponse>> ListAsync(ProductoSearchCriteria search, CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT "Id", "Codigo", "Nombre", "Precio", "Stock", "Categoria", "ImagePath", "CreatedAtUtc", "UpdatedAtUtc"
            FROM "Productos"
            """;

        var filters = new List<string>();
        var parameters = new DynamicParameters();

        if (search.Id.HasValue)
        {
            filters.Add("\"Id\" = @Id");
            parameters.Add("Id", search.Id.Value);
        }

        if (!string.IsNullOrWhiteSpace(search.Codigo))
        {
            filters.Add("\"Codigo\" ILIKE @Codigo");
            parameters.Add("Codigo", $"%{search.Codigo.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(search.Nombre))
        {
            filters.Add("\"Nombre\" ILIKE @Nombre");
            parameters.Add("Nombre", $"%{search.Nombre.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(search.Categoria))
        {
            filters.Add("\"Categoria\" ILIKE @Categoria");
            parameters.Add("Categoria", $"%{search.Categoria.Trim()}%");
        }

        if (search.Precio.HasValue)
        {
            filters.Add("\"Precio\" = @Precio");
            parameters.Add("Precio", search.Precio.Value);
        }

        if (search.Stock.HasValue)
        {
            filters.Add("\"Stock\" = @Stock");
            parameters.Add("Stock", search.Stock.Value);
        }

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
