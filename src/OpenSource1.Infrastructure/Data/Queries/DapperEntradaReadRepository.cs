using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.Entradas;
using OpenSource1.Application.Features.Entradas.Dtos;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperEntradaReadRepository(IDbConnectionFactory connectionFactory) : IEntradaReadRepository
{
    public async Task<EntradaResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id", "Titulo", "Descripcion", "Tipo", "Estado", "CreatedAtUtc", "UpdatedAtUtc"
            FROM "Entradas"
            WHERE "Id" = @Id
            """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<EntradaResponse>(command);
    }

    public async Task<IReadOnlyList<EntradaResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id", "Titulo", "Descripcion", "Tipo", "Estado", "CreatedAtUtc", "UpdatedAtUtc"
            FROM "Entradas"
            ORDER BY "CreatedAtUtc" DESC
            """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        var result = await connection.QueryAsync<EntradaResponse>(command);
        return result.AsList();
    }
}
