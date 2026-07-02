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
            SELECT "Id", "NombreCompleto", "DocumentoIdentidad", "Email", "Telefono", "Direccion", "Activo", "CreatedAtUtc", "UpdatedAtUtc"
            FROM "Clientes"
            WHERE "Id" = @Id
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ClienteResponse>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ClienteResponse>> ListAsync(ClienteSearchCriteria search, CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT "Id", "NombreCompleto", "DocumentoIdentidad", "Email", "Telefono", "Direccion", "Activo", "CreatedAtUtc", "UpdatedAtUtc"
            FROM "Clientes"
            """;

        var filters = new List<string>();
        var parameters = new DynamicParameters();

        if (search.Id.HasValue)
        {
            filters.Add("\"Id\" = @Id");
            parameters.Add("Id", search.Id.Value);
        }

        if (!string.IsNullOrWhiteSpace(search.DocumentoIdentidad))
        {
            filters.Add("\"DocumentoIdentidad\" ILIKE @DocumentoIdentidad");
            parameters.Add("DocumentoIdentidad", $"%{search.DocumentoIdentidad.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(search.Nombre))
        {
            filters.Add("\"NombreCompleto\" ILIKE @Nombre");
            parameters.Add("Nombre", $"%{search.Nombre.Trim()}%");
        }

        if (search.Activo.HasValue)
        {
            filters.Add("\"Activo\" = @Activo");
            parameters.Add("Activo", search.Activo.Value);
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
