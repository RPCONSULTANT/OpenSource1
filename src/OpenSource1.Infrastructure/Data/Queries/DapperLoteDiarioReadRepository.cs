using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.DiariosInventario.Lotes;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperLoteDiarioReadRepository(IDbSession session) : ILoteDiarioReadRepository
{
    private static readonly ColumnasPermitidas ColumnasPermitidas = new("Codigo", "Nombre", "CreatedAtUtc");

    // NumeroLineas: subconsulta correlacionada (mismo patrón que CategoriaPadreNombre en
    // DapperCategoriaProductoReadRepository) para que las columnas propias del lote ("Codigo", "Nombre"...) no sean
    // ambiguas al filtrar/ordenar sin alias de tabla.
    private const string Columnas = """
        "Id", "PlantillaDiarioId", "Codigo", "Nombre", "SerieId", "Bloqueado",
        (SELECT COUNT(*) FROM "LineasDiario" ln WHERE ln."LoteDiarioId" = "LotesDiario"."Id" AND ln."IsDeleted" = false) AS "NumeroLineas",
        "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy", xmin::text::bigint AS "Xmin"
        """;

    public async Task<LoteDiarioResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {Columnas}
            FROM "LotesDiario"
            WHERE "Id" = @Id AND "IsDeleted" = false
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, session.CurrentTransaction, cancellationToken: cancellationToken);
        return await session.Connection.QuerySingleOrDefaultAsync<LoteDiarioResponse>(command);
    }

    public async Task<Result<PagedResult<LoteDiarioResponse>>> ListAsync(
        LoteDiarioSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        var pagina = paginacion.Normalizar();

        var filters = new List<string>();
        var parameters = new DynamicParameters();

        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Codigo", search.Codigo);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Nombre", search.Nombre);

        if (search.PlantillaDiarioId is { } plantillaId)
        {
            filters.Add("\"PlantillaDiarioId\" = @PlantillaDiarioId");
            parameters.Add("PlantillaDiarioId", plantillaId);
        }

        filters.Insert(0, "\"IsDeleted\" = false");
        var whereSql = Environment.NewLine + "WHERE " + string.Join(" AND ", filters);

        var ordenColumna = ColumnasPermitidas.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "CreatedAtUtc";
        var ordenSql = ColumnasPermitidas.Citar(ordenColumna);
        var direccionSql = pagina.Descendente ? "DESC" : "ASC";

        parameters.Add("TamanoPagina", pagina.TamanoPagina);
        parameters.Add("Offset", pagina.Offset);

        var countSql = $"""
            SELECT COUNT(*) FROM "LotesDiario"
            {whereSql}
            """;

        var pageSql = $"""
            SELECT {Columnas}
            FROM "LotesDiario"
            {whereSql}
            ORDER BY {ordenSql} {direccionSql}, "Id" ASC
            LIMIT @TamanoPagina OFFSET @Offset
            """;

        await session.EnsureOpenAsync(cancellationToken);

        var total = await session.Connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        var items = await session.Connection.QueryAsync<LoteDiarioResponse>(
            new CommandDefinition(pageSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        return Result<PagedResult<LoteDiarioResponse>>.Exito(
            new PagedResult<LoteDiarioResponse>(items.AsList(), pagina.Pagina, pagina.TamanoPagina, total));
    }
}
