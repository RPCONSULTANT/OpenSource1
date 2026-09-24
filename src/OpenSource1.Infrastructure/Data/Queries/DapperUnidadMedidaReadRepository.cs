using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.UnidadesMedida;
using OpenSource1.Application.Features.UnidadesMedida.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperUnidadMedidaReadRepository(IDbSession session) : IUnidadMedidaReadRepository
{
    private static readonly ColumnasPermitidas ColumnasPermitidas = new("Codigo", "Nombre", "Decimales", "CreatedAtUtc");

    public async Task<UnidadMedidaResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id", "Codigo", "Nombre", "Decimales", "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"
            FROM "UnidadesMedida"
            WHERE "Id" = @Id AND "IsDeleted" = false
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, session.CurrentTransaction, cancellationToken: cancellationToken);
        return await session.Connection.QuerySingleOrDefaultAsync<UnidadMedidaResponse>(command);
    }

    public async Task<Result<PagedResult<UnidadMedidaResponse>>> ListAsync(
        UnidadMedidaSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        var pagina = paginacion.Normalizar();

        var filters = new List<string>();
        var parameters = new DynamicParameters();

        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Codigo", search.Codigo);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Nombre", search.Nombre);

        filters.Insert(0, "\"IsDeleted\" = false");
        var whereSql = Environment.NewLine + "WHERE " + string.Join(" AND ", filters);

        var ordenColumna = ColumnasPermitidas.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "CreatedAtUtc";
        var ordenSql = ColumnasPermitidas.Citar(ordenColumna);
        var direccionSql = pagina.Descendente ? "DESC" : "ASC";

        parameters.Add("TamanoPagina", pagina.TamanoPagina);
        parameters.Add("Offset", pagina.Offset);

        var countSql = $"""
            SELECT COUNT(*) FROM "UnidadesMedida"
            {whereSql}
            """;

        var pageSql = $"""
            SELECT "Id", "Codigo", "Nombre", "Decimales", "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"
            FROM "UnidadesMedida"
            {whereSql}
            ORDER BY {ordenSql} {direccionSql}, "Id" ASC
            LIMIT @TamanoPagina OFFSET @Offset
            """;

        await session.EnsureOpenAsync(cancellationToken);

        var total = await session.Connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        var items = await session.Connection.QueryAsync<UnidadMedidaResponse>(
            new CommandDefinition(pageSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        return Result<PagedResult<UnidadMedidaResponse>>.Exito(
            new PagedResult<UnidadMedidaResponse>(items.AsList(), pagina.Pagina, pagina.TamanoPagina, total));
    }
}
