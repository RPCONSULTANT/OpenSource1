using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.GruposContables;
using OpenSource1.Application.Features.GruposContables.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Infrastructure.Data.Queries;

/// <summary>
/// Lectura genérica de los cinco grupos contables simples. La tabla sale de <see cref="TiposGrupoContable"/> (catálogo fijo en
/// código, indexado por el enum): nunca es texto del usuario, así que interpolarla en el SQL es seguro. Mismo patrón de
/// filtros/orden/paginación que <see cref="DapperCuentaContableReadRepository"/>; orden por defecto <c>Codigo</c> ascendente.
/// </summary>
public sealed class DapperGrupoContableReadRepository(IDbSession session) : IGrupoContableReadRepository
{
    private static readonly ColumnasPermitidas ColumnasPermitidas = new("Codigo", "Descripcion", "CreatedAtUtc");

    private static string Columnas(TipoGrupoContable tipo) => $"""
        "Id", {(short)tipo}::smallint AS "Tipo", "Codigo", "Descripcion",
        xmin::text::bigint AS "Xmin", "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"
        """;

    private static string Tabla(TipoGrupoContable tipo) => $"\"{TiposGrupoContable.De(tipo).Tabla}\"";

    public async Task<GrupoContableResponse?> GetByIdAsync(TipoGrupoContable tipo, Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {Columnas(tipo)}
            FROM {Tabla(tipo)}
            WHERE "Id" = @Id AND "IsDeleted" = false
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, session.CurrentTransaction, cancellationToken: cancellationToken);
        return await session.Connection.QuerySingleOrDefaultAsync<GrupoContableResponse>(command);
    }

    public async Task<Result<PagedResult<GrupoContableResponse>>> ListAsync(
        TipoGrupoContable tipo, GrupoContableSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        var pagina = paginacion.Normalizar();

        var filters = new List<string>();
        var parameters = new DynamicParameters();

        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Codigo", search.Codigo);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Descripcion", search.Descripcion);

        filters.Insert(0, "\"IsDeleted\" = false");
        var whereSql = Environment.NewLine + "WHERE " + string.Join(" AND ", filters);

        var ordenColumna = ColumnasPermitidas.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "Codigo";
        var ordenSql = ColumnasPermitidas.Citar(ordenColumna);
        var direccionSql = pagina.Descendente ? "DESC" : "ASC";

        parameters.Add("TamanoPagina", pagina.TamanoPagina);
        parameters.Add("Offset", pagina.Offset);

        var countSql = $"""
            SELECT COUNT(*) FROM {Tabla(tipo)}
            {whereSql}
            """;

        var pageSql = $"""
            SELECT {Columnas(tipo)}
            FROM {Tabla(tipo)}
            {whereSql}
            ORDER BY {ordenSql} {direccionSql}, "Id" ASC
            LIMIT @TamanoPagina OFFSET @Offset
            """;

        await session.EnsureOpenAsync(cancellationToken);

        var total = await session.Connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        var items = await session.Connection.QueryAsync<GrupoContableResponse>(
            new CommandDefinition(pageSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        return Result<PagedResult<GrupoContableResponse>>.Exito(
            new PagedResult<GrupoContableResponse>(items.AsList(), pagina.Pagina, pagina.TamanoPagina, total));
    }
}
