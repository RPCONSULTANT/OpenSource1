using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.Clientes;
using OpenSource1.Application.Features.Clientes.Dtos;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperClienteReadRepository(IDbConnectionFactory connectionFactory) : IClienteReadRepository
{
    public async Task<ClienteResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id", "Nombre", "Apellido", "Email", "Telefono", "Direccion", "ImagePath", "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"
            FROM "Clientes"
            WHERE "Id" = @Id
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ClienteResponse>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ClienteResponse>> ListAsync(ClienteSearchCriteria search, CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT "Id", "Nombre", "Apellido", "Email", "Telefono", "Direccion", "ImagePath", "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"
            FROM "Clientes"
            """;

        var filters = new List<string>();
        var parameters = new DynamicParameters();

        FilterExpressionBuilder.AddTextFilter(filters, parameters, "Nombre", search.Nombre);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, "Apellido", search.Apellido);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, "Email", search.Email);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, "Telefono", search.Telefono);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, "Direccion", search.Direccion);

        if (filters.Count > 0)
        {
            sql += Environment.NewLine + "WHERE " + string.Join(" AND ", filters);
        }

        sql += Environment.NewLine + "ORDER BY \"CreatedAtUtc\" DESC";

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var result = await connection.QueryAsync<ClienteResponse>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return result.AsList();
    }
}
