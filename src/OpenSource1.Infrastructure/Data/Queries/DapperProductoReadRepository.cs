using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.Productos;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperProductoReadRepository(IDbConnectionFactory connectionFactory) : IProductoReadRepository
{
    private static readonly ColumnasPermitidas ColumnasPermitidas = new(
        "Codigo", "Nombre", "CategoriaCodigo", "CategoriaNombre", "UnidadMedidaCodigo", "UnidadMedidaNombre", "Precio", "Stock");

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
            throw new ErroresDeDominioException(precioResult.Errores[0]);
        }

        var stockResult = FilterExpressionBuilder.AddExactFilter(filters, parameters, ColumnasPermitidas, "Stock", search.Stock,
            static term => (int.TryParse(term, out var value), value));
        if (stockResult.EsFallo)
        {
            throw new ErroresDeDominioException(stockResult.Errores[0]);
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
