using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.CuentasContables;
using OpenSource1.Application.Features.CuentasContables.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperCuentaContableReadRepository(IDbSession session) : ICuentaContableReadRepository
{
    private static readonly ColumnasPermitidas ColumnasPermitidas = new("Numero", "Nombre", "CreatedAtUtc");

    private const string Columnas = """
        "Id", "Numero", "Nombre", "TipoCuenta", "TipoResultado", "PosteoDirecto", "Bloqueada", "Sangria",
        xmin::text::bigint AS "Xmin", "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"
        """;

    public async Task<CuentaContableResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT {Columnas}
            FROM "CuentasContables"
            WHERE "Id" = @Id AND "IsDeleted" = false
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, session.CurrentTransaction, cancellationToken: cancellationToken);
        return await session.Connection.QuerySingleOrDefaultAsync<CuentaContableResponse>(command);
    }

    public async Task<Result<PagedResult<CuentaContableResponse>>> ListAsync(
        CuentaContableSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        var pagina = paginacion.Normalizar();

        var filters = new List<string>();
        var parameters = new DynamicParameters();

        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Numero", search.Numero);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Nombre", search.Nombre);

        if (search.TipoCuenta is { } tipoCuenta)
        {
            filters.Add("\"TipoCuenta\" = @TipoCuenta");
            parameters.Add("TipoCuenta", (short)tipoCuenta);
        }

        if (search.TipoResultado is { } tipoResultado)
        {
            filters.Add("\"TipoResultado\" = @TipoResultado");
            parameters.Add("TipoResultado", (short)tipoResultado);
        }

        if (search.Bloqueada is { } bloqueada)
        {
            filters.Add("\"Bloqueada\" = @Bloqueada");
            parameters.Add("Bloqueada", bloqueada);
        }

        filters.Insert(0, "\"IsDeleted\" = false");
        var whereSql = Environment.NewLine + "WHERE " + string.Join(" AND ", filters);

        // Por defecto, el plan de cuentas se lee en su orden natural (Numero ascendente), no por fecha de alta.
        var ordenColumna = ColumnasPermitidas.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "Numero";
        var ordenSql = ColumnasPermitidas.Citar(ordenColumna);
        var direccionSql = pagina.Descendente ? "DESC" : "ASC";

        parameters.Add("TamanoPagina", pagina.TamanoPagina);
        parameters.Add("Offset", pagina.Offset);

        var countSql = $"""
            SELECT COUNT(*) FROM "CuentasContables"
            {whereSql}
            """;

        var pageSql = $"""
            SELECT {Columnas}
            FROM "CuentasContables"
            {whereSql}
            ORDER BY {ordenSql} {direccionSql}, "Id" ASC
            LIMIT @TamanoPagina OFFSET @Offset
            """;

        await session.EnsureOpenAsync(cancellationToken);

        var total = await session.Connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        var items = await session.Connection.QueryAsync<CuentaContableResponse>(
            new CommandDefinition(pageSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        return Result<PagedResult<CuentaContableResponse>>.Exito(
            new PagedResult<CuentaContableResponse>(items.AsList(), pagina.Pagina, pagina.TamanoPagina, total));
    }
}
