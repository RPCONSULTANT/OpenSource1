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
            SELECT "Id", "Nombre", "Apellido", "Email", "Telefono", "Direccion", "ImagePath", "CreatedAtUtc", "UpdatedAtUtc"
            FROM "Clientes"
            WHERE "Id" = @Id
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ClienteResponse>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ClienteResponse>> ListAsync(ClienteSearchCriteria search, CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT "Id", "Nombre", "Apellido", "Email", "Telefono", "Direccion", "ImagePath", "CreatedAtUtc", "UpdatedAtUtc"
            FROM "Clientes"
            """;

        var filters = new List<string>();
        var parameters = new DynamicParameters();

        if (search.Id.HasValue)
        {
            filters.Add("\"Id\" = @Id");
            parameters.Add("Id", search.Id.Value);
        }

        if (!string.IsNullOrWhiteSpace(search.Nombre))
        {
            filters.Add("\"Nombre\" ILIKE @Nombre");
            parameters.Add("Nombre", $"%{search.Nombre.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(search.Apellido))
        {
            filters.Add("\"Apellido\" ILIKE @Apellido");
            parameters.Add("Apellido", $"%{search.Apellido.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(search.Email))
        {
            filters.Add("\"Email\" ILIKE @Email");
            parameters.Add("Email", $"%{search.Email.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(search.Telefono))
        {
            filters.Add("\"Telefono\" ILIKE @Telefono");
            parameters.Add("Telefono", $"%{search.Telefono.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(search.Direccion))
        {
            filters.Add("\"Direccion\" ILIKE @Direccion");
            parameters.Add("Direccion", $"%{search.Direccion.Trim()}%");
        }

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
