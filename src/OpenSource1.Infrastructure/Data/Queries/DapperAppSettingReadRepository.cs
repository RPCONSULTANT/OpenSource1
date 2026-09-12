using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.AppSettings;
using OpenSource1.Application.Features.AppSettings.Dtos;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperAppSettingReadRepository(IDbSession session) : IAppSettingReadRepository
{
    public async Task<AppSettingResponse?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id", "Key", "Value", "Description", "CreatedAtUtc", "UpdatedAtUtc"
            FROM "AppSettings"
            WHERE "Key" = @Key
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Key = key }, session.CurrentTransaction, cancellationToken: cancellationToken);
        return await session.Connection.QuerySingleOrDefaultAsync<AppSettingResponse>(command);
    }

    public async Task<IReadOnlyList<AppSettingResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id", "Key", "Value", "Description", "CreatedAtUtc", "UpdatedAtUtc"
            FROM "AppSettings"
            ORDER BY "Key"
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, transaction: session.CurrentTransaction, cancellationToken: cancellationToken);
        var settings = await session.Connection.QueryAsync<AppSettingResponse>(command);
        return settings.AsList();
    }
}
