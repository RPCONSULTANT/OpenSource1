using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.Clientes;
using OpenSource1.Application.Features.Clientes.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperClienteReadRepository(IDbSession session) : IClienteReadRepository
{
    private static readonly ColumnasPermitidas ColumnasPermitidas = new(
        "Nombre", "Apellido", "Email", "Telefono", "DireccionLinea1", "Sector", "PaisNombre", "CreatedAtUtc");

    public async Task<ClienteResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id", "Nombre", "Apellido", "Email", "Telefono", "DireccionLinea1", "DireccionLinea2", "Sector", "PaisCodigo", "PaisNombre", "ImagePath", "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"
            FROM "Clientes"
            WHERE "Id" = @Id AND "IsDeleted" = false
            """;
        await session.EnsureOpenAsync(cancellationToken);
        return await session.Connection.QuerySingleOrDefaultAsync<ClienteResponse>(new CommandDefinition(sql, new { Id = id }, session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    public async Task<Result<PagedResult<ClienteResponse>>> ListAsync(
        ClienteSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        var pagina = paginacion.Normalizar();

        var filters = new List<string>();
        var parameters = new DynamicParameters();

        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Nombre", search.Nombre);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Apellido", search.Apellido);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Email", search.Email);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Telefono", search.Telefono);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "DireccionLinea1", search.DireccionLinea1);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Sector", search.Sector);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "PaisNombre", search.PaisNombre);

        filters.Insert(0, "\"IsDeleted\" = false");
        var whereSql = Environment.NewLine + "WHERE " + string.Join(" AND ", filters);

        var ordenColumna = ColumnasPermitidas.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "CreatedAtUtc";
        var ordenSql = ColumnasPermitidas.Citar(ordenColumna);
        var direccionSql = pagina.Descendente ? "DESC" : "ASC";

        parameters.Add("TamanoPagina", pagina.TamanoPagina);
        parameters.Add("Offset", pagina.Offset);

        var countSql = $"""
            SELECT COUNT(*) FROM "Clientes"
            {whereSql}
            """;

        var pageSql = $"""
            SELECT "Id", "Nombre", "Apellido", "Email", "Telefono", "DireccionLinea1", "DireccionLinea2", "Sector", "PaisCodigo", "PaisNombre", "ImagePath", "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"
            FROM "Clientes"
            {whereSql}
            ORDER BY {ordenSql} {direccionSql}, "Id" ASC
            LIMIT @TamanoPagina OFFSET @Offset
            """;

        await session.EnsureOpenAsync(cancellationToken);

        var total = await session.Connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        var items = await session.Connection.QueryAsync<ClienteResponse>(
            new CommandDefinition(pageSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        return Result<PagedResult<ClienteResponse>>.Exito(
            new PagedResult<ClienteResponse>(items.AsList(), pagina.Pagina, pagina.TamanoPagina, total));
    }
}
