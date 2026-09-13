using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.Entradas;
using OpenSource1.Application.Features.Entradas.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperEntradaReadRepository(IDbSession session) : IEntradaReadRepository
{
    private static readonly ColumnasPermitidas ColumnasPermitidas = new("CreatedAtUtc", "Titulo", "Tipo", "Estado");

    public async Task<EntradaResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id", "Titulo", "Descripcion", "Tipo", "Estado", "CreatedAtUtc", "UpdatedAtUtc"
            FROM "Entradas"
            WHERE "Id" = @Id AND "IsDeleted" = false
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, session.CurrentTransaction, cancellationToken: cancellationToken);
        return await session.Connection.QuerySingleOrDefaultAsync<EntradaResponse>(command);
    }

    public async Task<Result<PagedResult<EntradaResponse>>> ListAsync(
        PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        var pagina = paginacion.Normalizar();

        var ordenColumna = ColumnasPermitidas.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "CreatedAtUtc";
        var ordenSql = ColumnasPermitidas.Citar(ordenColumna);
        var direccionSql = pagina.Descendente ? "DESC" : "ASC";

        const string countSql = """
            SELECT COUNT(*) FROM "Entradas"
            WHERE "IsDeleted" = false
            """;

        var pageSql = $"""
            SELECT "Id", "Titulo", "Descripcion", "Tipo", "Estado", "CreatedAtUtc", "UpdatedAtUtc"
            FROM "Entradas"
            WHERE "IsDeleted" = false
            ORDER BY {ordenSql} {direccionSql}
            LIMIT @TamanoPagina OFFSET @Offset
            """;

        await session.EnsureOpenAsync(cancellationToken);

        var total = await session.Connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, transaction: session.CurrentTransaction, cancellationToken: cancellationToken));

        var items = await session.Connection.QueryAsync<EntradaResponse>(
            new CommandDefinition(pageSql, new { pagina.TamanoPagina, pagina.Offset }, session.CurrentTransaction, cancellationToken: cancellationToken));

        return Result<PagedResult<EntradaResponse>>.Exito(
            new PagedResult<EntradaResponse>(items.AsList(), pagina.Pagina, pagina.TamanoPagina, total));
    }
}
