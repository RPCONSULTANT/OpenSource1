using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.DiariosInventario.Registros;
using OpenSource1.Application.Features.DiariosInventario.Registros.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

/// <summary>Listado de registros de diario (append-only), del más reciente al más antiguo por <c>Id</c>.</summary>
public sealed class DapperRegistroDiarioReadRepository(IDbSession session) : IRegistroDiarioReadRepository
{
    public async Task<Result<PagedResult<RegistroDiarioResponse>>> ListAsync(
        Guid? loteDiarioId, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        var pagina = paginacion.Normalizar();
        var parameters = new DynamicParameters();
        var whereSql = string.Empty;
        if (loteDiarioId is { } loteId)
        {
            whereSql = """WHERE r."LoteDiarioId" = @LoteDiarioId""";
            parameters.Add("LoteDiarioId", loteId);
        }

        parameters.Add("TamanoPagina", pagina.TamanoPagina);
        parameters.Add("Offset", pagina.Offset);

        var countSql = $"""SELECT COUNT(*) FROM "RegistrosDiario" r {whereSql}""";

        // LEFT JOIN sin filtro de IsDeleted: un registro sigue mostrando el código de su lote aunque este se borre.
        var direccion = pagina.Descendente ? "DESC" : "ASC";
        var pageSql = $"""
            SELECT r."Id", r."NumeroRegistro", r."LoteDiarioId", l."Codigo" AS "LoteDiarioCodigo",
                   r."DesdeMovimientoProducto", r."HastaMovimientoProducto", r."Lineas", r."FechaCreacion", r."CreadoPor"
            FROM "RegistrosDiario" r
            LEFT JOIN "LotesDiario" l ON l."Id" = r."LoteDiarioId"
            {whereSql}
            ORDER BY r."Id" {direccion}
            LIMIT @TamanoPagina OFFSET @Offset
            """;

        await session.EnsureOpenAsync(cancellationToken);

        var total = await session.Connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));
        var items = await session.Connection.QueryAsync<RegistroDiarioResponse>(
            new CommandDefinition(pageSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        return Result<PagedResult<RegistroDiarioResponse>>.Exito(
            new PagedResult<RegistroDiarioResponse>(items.AsList(), pagina.Pagina, pagina.TamanoPagina, total));
    }
}
