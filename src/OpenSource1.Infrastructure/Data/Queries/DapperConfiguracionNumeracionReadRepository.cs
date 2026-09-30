using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.ConfiguracionNumeracion;
using OpenSource1.Application.Features.ConfiguracionNumeracion.Dtos;
using OpenSource1.Core.Enums;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperConfiguracionNumeracionReadRepository(IDbSession session) : IConfiguracionNumeracionReadRepository
{
    public async Task<IReadOnlyList<ConfiguracionNumeracionResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);
        var filas = await session.Connection.QueryAsync<Fila>(new CommandDefinition(
            """
            SELECT c."TipoDocumento", c."SerieId", s."Codigo" AS "SerieCodigo", s."Descripcion" AS "SerieDescripcion",
                   s."Activa" AS "SerieActiva", c.xmin::text::bigint AS "Xmin"
            FROM "ConfiguracionesNumeracion" c JOIN "Series" s ON s."Id" = c."SerieId"
            WHERE c."IsDeleted" = false
            ORDER BY c."TipoDocumento"
            """,
            transaction: session.CurrentTransaction, cancellationToken: cancellationToken));
        return [.. filas.Select(f => new ConfiguracionNumeracionResponse
        {
            TipoDocumento = (TipoDocumentoSerie)f.TipoDocumento,
            SerieId = f.SerieId,
            SerieCodigo = f.SerieCodigo,
            SerieDescripcion = f.SerieDescripcion,
            SerieActiva = f.SerieActiva,
            Xmin = f.Xmin,
        })];
    }

    private sealed record Fila(short TipoDocumento, Guid SerieId, string SerieCodigo, string SerieDescripcion, bool SerieActiva, long Xmin);
}
