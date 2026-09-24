using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.SociosNegocio;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperSocioNegocioReadRepository(IDbSession session) : ISocioNegocioReadRepository
{
    private static readonly ColumnasPermitidas ColumnasPermitidas = new(
        "Codigo", "NombreComercial", "Email", "Telefono", "DireccionLinea1", "Sector", "PaisNombre", "CreatedAtUtc");

    public async Task<SocioNegocioResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT "Id", "Codigo", "Tipo", "NombreComercial", "RazonSocial", "TipoDocumentoFiscal", "NumeroDocumentoFiscal", "Email", "Telefono", "DireccionLinea1", "DireccionLinea2", "Ciudad", "Sector", "PaisCodigo", "PaisNombre", "TerminoPagoId", "LimiteCredito", "Bloqueado", "ImagePath", "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"
            FROM "SociosNegocio"
            WHERE "Id" = @Id AND "IsDeleted" = false
            """;
        await session.EnsureOpenAsync(cancellationToken);
        return await session.Connection.QuerySingleOrDefaultAsync<SocioNegocioResponse>(new CommandDefinition(sql, new { Id = id }, session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    public async Task<Result<PagedResult<SocioNegocioResponse>>> ListAsync(
        SocioNegocioSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        var pagina = paginacion.Normalizar();

        var filters = new List<string>();
        var parameters = new DynamicParameters();

        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "NombreComercial", search.NombreComercial);
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
            SELECT COUNT(*) FROM "SociosNegocio"
            {whereSql}
            """;

        var pageSql = $"""
            SELECT "Id", "Codigo", "Tipo", "NombreComercial", "RazonSocial", "TipoDocumentoFiscal", "NumeroDocumentoFiscal", "Email", "Telefono", "DireccionLinea1", "DireccionLinea2", "Ciudad", "Sector", "PaisCodigo", "PaisNombre", "TerminoPagoId", "LimiteCredito", "Bloqueado", "ImagePath", "CreatedAtUtc", "UpdatedAtUtc", "CreatedBy", "UpdatedBy"
            FROM "SociosNegocio"
            {whereSql}
            ORDER BY {ordenSql} {direccionSql}, "Id" ASC
            LIMIT @TamanoPagina OFFSET @Offset
            """;

        await session.EnsureOpenAsync(cancellationToken);

        var total = await session.Connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        var items = await session.Connection.QueryAsync<SocioNegocioResponse>(
            new CommandDefinition(pageSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        return Result<PagedResult<SocioNegocioResponse>>.Exito(
            new PagedResult<SocioNegocioResponse>(items.AsList(), pagina.Pagina, pagina.TamanoPagina, total));
    }
}
