using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.AppSettings;
using OpenSource1.Application.Features.AppSettings.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperAppSettingReadRepository(IDbSession session) : IAppSettingReadRepository
{
    private static readonly ColumnasPermitidas ColumnasPermitidas = new("CreatedAtUtc", "Key");

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

    public async Task<Result<PagedResult<AppSettingResponse>>> ListAsync(
        PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        var pagina = paginacion.Normalizar();

        var ordenColumna = ColumnasPermitidas.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "CreatedAtUtc";
        var ordenSql = ColumnasPermitidas.Citar(ordenColumna);
        var direccionSql = pagina.Descendente ? "DESC" : "ASC";

        const string countSql = """
            SELECT COUNT(*) FROM "AppSettings"
            """;

        var pageSql = $"""
            SELECT "Id", "Key", "Value", "Description", "CreatedAtUtc", "UpdatedAtUtc"
            FROM "AppSettings"
            ORDER BY {ordenSql} {direccionSql}
            LIMIT @TamanoPagina OFFSET @Offset
            """;

        await session.EnsureOpenAsync(cancellationToken);

        var total = await session.Connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, transaction: session.CurrentTransaction, cancellationToken: cancellationToken));

        var settings = await session.Connection.QueryAsync<AppSettingResponse>(
            new CommandDefinition(pageSql, new { pagina.TamanoPagina, pagina.Offset }, session.CurrentTransaction, cancellationToken: cancellationToken));

        return Result<PagedResult<AppSettingResponse>>.Exito(
            new PagedResult<AppSettingResponse>(settings.AsList(), pagina.Pagina, pagina.TamanoPagina, total));
    }
}
