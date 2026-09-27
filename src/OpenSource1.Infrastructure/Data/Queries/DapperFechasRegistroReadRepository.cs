using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.FechasRegistro;
using OpenSource1.Application.Features.FechasRegistro.Dtos;
using OpenSource1.Core.Entities;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperFechasRegistroReadRepository(IDbSession session) : IFechasRegistroReadRepository
{
    private const string ColumnasUsuario = """
        "Id", "UsuarioId", "NombreUsuario", "PermitirRegistroDesde", "PermitirRegistroHasta",
        "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"
        """;

    public async Task<FechasRegistroGeneralResponse?> GetGeneralAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "PermitirRegistroDesde", "PermitirRegistroHasta", "UpdatedAtUtc", "UpdatedBy"
            FROM "ConfiguracionesRegistro"
            WHERE "Id" = @Id AND "IsDeleted" = false
            """;

        await session.EnsureOpenAsync(cancellationToken);
        return await session.Connection.QuerySingleOrDefaultAsync<FechasRegistroGeneralResponse>(new CommandDefinition(
            sql, new { Id = ConfiguracionRegistroIds.General }, session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<FechasRegistroUsuarioResponse>> ListUsuariosAsync(CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT {ColumnasUsuario}
            FROM "ConfiguracionesRegistroUsuario"
            WHERE "IsDeleted" = false
            ORDER BY "NombreUsuario" ASC, "Id" ASC
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var filas = await session.Connection.QueryAsync<FechasRegistroUsuarioResponse>(new CommandDefinition(
            sql, transaction: session.CurrentTransaction, cancellationToken: cancellationToken));
        return [.. filas];
    }

    public async Task<FechasRegistroUsuarioResponse?> GetUsuarioByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT {ColumnasUsuario}
            FROM "ConfiguracionesRegistroUsuario"
            WHERE "Id" = @Id AND "IsDeleted" = false
            """;

        await session.EnsureOpenAsync(cancellationToken);
        return await session.Connection.QuerySingleOrDefaultAsync<FechasRegistroUsuarioResponse>(new CommandDefinition(
            sql, new { Id = id }, session.CurrentTransaction, cancellationToken: cancellationToken));
    }
}
