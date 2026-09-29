using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.DiariosInventario.Plantillas;
using OpenSource1.Application.Features.DiariosInventario.Plantillas.Dtos;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperPlantillaDiarioReadRepository(IDbSession session) : IPlantillaDiarioReadRepository
{
    public async Task<IReadOnlyList<PlantillaDiarioResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id", "Codigo", "Nombre", "Tipo", "SerieId"
            FROM "PlantillasDiario"
            WHERE "IsDeleted" = false
            ORDER BY "Codigo" ASC
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var items = await session.Connection.QueryAsync<PlantillaDiarioResponse>(
            new CommandDefinition(sql, transaction: session.CurrentTransaction, cancellationToken: cancellationToken));
        return items.AsList();
    }
}
