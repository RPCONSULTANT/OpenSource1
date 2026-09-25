using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.Almacenes;
using OpenSource1.Application.Features.Almacenes.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperAlmacenReadRepository(IDbSession session) : IAlmacenReadRepository
{
    private static readonly ColumnasPermitidas ColumnasPermitidas = new("Codigo", "Nombre", "CreatedAtUtc");

    private const string Columnas = """
        "Id", "Codigo", "Nombre", "DireccionLinea1", "DireccionLinea2", "Ciudad", "PaisCodigo",
        "Bloqueado", "EsPredeterminado", "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"
        """;

    public async Task<AlmacenResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {Columnas}
            FROM "Almacenes"
            WHERE "Id" = @Id AND "IsDeleted" = false
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, session.CurrentTransaction, cancellationToken: cancellationToken);
        return await session.Connection.QuerySingleOrDefaultAsync<AlmacenResponse>(command);
    }

    public async Task<Result<PagedResult<AlmacenResponse>>> ListAsync(
        AlmacenSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        var pagina = paginacion.Normalizar();

        var filters = new List<string>();
        var parameters = new DynamicParameters();

        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Codigo", search.Codigo);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Nombre", search.Nombre);

        if (search.Bloqueado is { } bloqueado)
        {
            filters.Add("\"Bloqueado\" = @Bloqueado");
            parameters.Add("Bloqueado", bloqueado);
        }

        filters.Insert(0, "\"IsDeleted\" = false");
        var whereSql = Environment.NewLine + "WHERE " + string.Join(" AND ", filters);

        var ordenColumna = ColumnasPermitidas.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "CreatedAtUtc";
        var ordenSql = ColumnasPermitidas.Citar(ordenColumna);
        var direccionSql = pagina.Descendente ? "DESC" : "ASC";

        parameters.Add("TamanoPagina", pagina.TamanoPagina);
        parameters.Add("Offset", pagina.Offset);

        var countSql = $"""
            SELECT COUNT(*) FROM "Almacenes"
            {whereSql}
            """;

        var pageSql = $"""
            SELECT {Columnas}
            FROM "Almacenes"
            {whereSql}
            ORDER BY {ordenSql} {direccionSql}, "Id" ASC
            LIMIT @TamanoPagina OFFSET @Offset
            """;

        await session.EnsureOpenAsync(cancellationToken);

        var total = await session.Connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        var items = await session.Connection.QueryAsync<AlmacenResponse>(
            new CommandDefinition(pageSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        return Result<PagedResult<AlmacenResponse>>.Exito(
            new PagedResult<AlmacenResponse>(items.AsList(), pagina.Pagina, pagina.TamanoPagina, total));
    }
}
