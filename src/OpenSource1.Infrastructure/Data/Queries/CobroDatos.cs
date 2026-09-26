using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.Cobros;
using OpenSource1.Core.Enums;

namespace OpenSource1.Infrastructure.Data.Queries;

/// <summary>Acceso a datos de los cobros (Task 6.5) con Dapper sobre la transacción de <see cref="IDbSession"/>.</summary>
public sealed class CobroDatos(IDbSession session) : ICobroDatos
{
    public async Task<IReadOnlyList<SocioCobro>> BloquearSociosAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (session.CurrentTransaction is null)
        {
            throw new InvalidOperationException("El bloqueo de socios de un cobro requiere una transacción activa.");
        }

        await session.EnsureOpenAsync(cancellationToken);

        // "IsDeleted" en el WHERE: si el socio se borra mientras se espera el bloqueo, PostgreSQL re-evalúa la fila bloqueada con su
        // versión nueva y deja de devolverla.
        var filas = await session.Connection.QueryAsync<SocioFila>(new CommandDefinition(
            """
            SELECT "Id", "Bloqueado", "GrupoClienteContableId"
            FROM "SociosNegocio"
            WHERE "Id" = ANY(@Ids) AND "IsDeleted" = false
            ORDER BY "Id"
            FOR SHARE
            """,
            new { Ids = ids.Distinct().Order().ToArray() }, session.CurrentTransaction, cancellationToken: cancellationToken));

        return [.. filas.Select(f => new SocioCobro(f.Id, (BloqueoSocioNegocio)f.Bloqueado, f.GrupoClienteContableId))];
    }

    private sealed class SocioFila
    {
        public Guid Id { get; init; }
        public short Bloqueado { get; init; }
        public Guid? GrupoClienteContableId { get; init; }
    }
}
