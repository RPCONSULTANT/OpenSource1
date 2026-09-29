using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.DiariosInventario.Lotes;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class LoteDiarioBloqueoService(IDbSession session) : ILoteDiarioBloqueoService
{
    public async Task<bool?> BloquearYObtenerEstadoAsync(Guid loteDiarioId, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);

        // Requiere transacción activa: FOR UPDATE sin transacción no retiene el bloqueo más allá del statement
        // (mismo motivo que GeneradorNumeroDocumento).
        if (session.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Bloquear un lote de diario requiere una transacción activa.");
        }

        const string sql = """
            SELECT "Bloqueado" FROM "LotesDiario" WHERE "Id" = @Id AND "IsDeleted" = false FOR UPDATE
            """;

        return await session.Connection.QuerySingleOrDefaultAsync<bool?>(
            new CommandDefinition(sql, new { Id = loteDiarioId }, session.CurrentTransaction, cancellationToken: cancellationToken));
    }
}
