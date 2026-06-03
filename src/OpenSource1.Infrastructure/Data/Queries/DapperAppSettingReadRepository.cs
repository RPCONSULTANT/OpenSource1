using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.AppSettings;
using OpenSource1.Application.Features.AppSettings.Dtos;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperAppSettingReadRepository(IDbConnectionFactory connectionFactory) : IAppSettingReadRepository
{
    public async Task<AppSettingResponse?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, [Key], [Value], [Description], CreatedAtUtc, UpdatedAtUtc
            FROM AppSettings
            WHERE [Key] = @Key
            """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Key = key }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<AppSettingResponse>(command);
    }

    public async Task<IReadOnlyList<AppSettingResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, [Key], [Value], [Description], CreatedAtUtc, UpdatedAtUtc
            FROM AppSettings
            ORDER BY [Key]
            """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        var settings = await connection.QueryAsync<AppSettingResponse>(command);
        return settings.AsList();
    }
}
