using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.Entradas;
using OpenSource1.Application.Features.Entradas.Dtos;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperEntradaReadRepository(IDbSession session) : IEntradaReadRepository
{
    public async Task<EntradaResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id", "Titulo", "Descripcion", "Tipo", "Estado", "CreatedAtUtc", "UpdatedAtUtc"
            FROM "Entradas"
            WHERE "Id" = @Id
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, session.CurrentTransaction, cancellationToken: cancellationToken);
        return await session.Connection.QuerySingleOrDefaultAsync<EntradaResponse>(command);
    }

    public async Task<IReadOnlyList<EntradaResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id", "Titulo", "Descripcion", "Tipo", "Estado", "CreatedAtUtc", "UpdatedAtUtc"
            FROM "Entradas"
            ORDER BY "CreatedAtUtc" DESC
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, transaction: session.CurrentTransaction, cancellationToken: cancellationToken);
        var result = await session.Connection.QueryAsync<EntradaResponse>(command);
        return result.AsList();
    }
}
