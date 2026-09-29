using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.FacturasVenta.Borradores;
using OpenSource1.Application.Features.FacturasVenta.Borradores.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

/// <summary>
/// Cabeceras de borradores con los códigos de socios/término/almacén y el conteo de líneas vivas. La consulta se aplana en
/// una subconsulta (<c>b</c>) para que los filtros y el orden nombren columnas sin calificar sin ambigüedad (mismo motivo que
/// <c>DapperProductoReadRepository</c>).
/// </summary>
public sealed class DapperFacturaVentaBorradorReadRepository(IDbSession session) : IFacturaVentaBorradorReadRepository
{
    private static readonly ColumnasPermitidas ColumnasPermitidas =
        new("Numero", "NombreFacturacion", "FechaDocumento", "FechaRegistro", "CreatedAtUtc");

    private const string Base = """
        SELECT f."Id", f."Numero", f."SocioNegocioId", sv."Codigo" AS "SocioNegocioCodigo", sv."NombreComercial" AS "SocioNegocioNombre",
               f."SocioNegocioFacturarAId", sf."Codigo" AS "SocioNegocioFacturarACodigo",
               f."NombreFacturacion", f."RazonSocialFacturacion", f."TipoDocumentoFiscal", f."NumeroDocumentoFiscal",
               f."DireccionFacturacionLinea1", f."DireccionFacturacionLinea2", f."CiudadFacturacion", f."PaisCodigoFacturacion",
               f."FechaRegistro", f."FechaDocumento", f."FechaVencimiento", f."TerminoPagoId", t."Codigo" AS "TerminoPagoCodigo",
               f."GrupoNegocioId", f."GrupoIvaNegocioId", f."GrupoClienteContableId", f."AlmacenId", a."Codigo" AS "AlmacenCodigo",
               f."Estado", f."Moneda", f."Descripcion",
               (SELECT COUNT(*) FROM "LineasFacturaVentaBorrador" l
                WHERE l."FacturaVentaBorradorId" = f."Id" AND l."IsDeleted" = false)::int AS "NumeroLineas",
               f.xmin::text::bigint AS "Xmin", f."CreatedAtUtc", f."UpdatedAtUtc", f."CreatedBy", f."UpdatedBy"
        FROM "FacturasVentaBorrador" f
        LEFT JOIN "SociosNegocio" sv ON sv."Id" = f."SocioNegocioId"
        LEFT JOIN "SociosNegocio" sf ON sf."Id" = f."SocioNegocioFacturarAId"
        LEFT JOIN "TerminosPago" t ON t."Id" = f."TerminoPagoId"
        LEFT JOIN "Almacenes" a ON a."Id" = f."AlmacenId"
        WHERE f."IsDeleted" = false
        """;

    public async Task<FacturaVentaBorradorResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT * FROM ({Base}) b
            WHERE "Id" = @Id
            """;

        await session.EnsureOpenAsync(cancellationToken);
        return await session.Connection.QuerySingleOrDefaultAsync<FacturaVentaBorradorResponse>(
            new CommandDefinition(sql, new { Id = id }, session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    public async Task<Result<PagedResult<FacturaVentaBorradorResponse>>> ListAsync(
        FacturaVentaBorradorSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        var pagina = paginacion.Normalizar();

        var filters = new List<string>();
        var parameters = new DynamicParameters();

        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Numero", search.Numero);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "NombreFacturacion", search.NombreFacturacion);

        if (search.SocioNegocioId is { } socioId)
        {
            filters.Add("(\"SocioNegocioId\" = @SocioNegocioId OR \"SocioNegocioFacturarAId\" = @SocioNegocioId)");
            parameters.Add("SocioNegocioId", socioId);
        }

        if (search.Estado is { } estado)
        {
            filters.Add("\"Estado\" = @Estado");
            parameters.Add("Estado", (short)estado);
        }

        var whereSql = filters.Count == 0 ? string.Empty : Environment.NewLine + "WHERE " + string.Join(" AND ", filters);

        var ordenColumna = ColumnasPermitidas.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "CreatedAtUtc";
        var ordenSql = ColumnasPermitidas.Citar(ordenColumna);
        var direccionSql = pagina.Descendente ? "DESC" : "ASC";

        parameters.Add("TamanoPagina", pagina.TamanoPagina);
        parameters.Add("Offset", pagina.Offset);

        var countSql = $"""
            SELECT COUNT(*) FROM ({Base}) b
            {whereSql}
            """;

        var pageSql = $"""
            SELECT * FROM ({Base}) b
            {whereSql}
            ORDER BY {ordenSql} {direccionSql}, "Id" ASC
            LIMIT @TamanoPagina OFFSET @Offset
            """;

        await session.EnsureOpenAsync(cancellationToken);

        var total = await session.Connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        var items = await session.Connection.QueryAsync<FacturaVentaBorradorResponse>(
            new CommandDefinition(pageSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        return Result<PagedResult<FacturaVentaBorradorResponse>>.Exito(
            new PagedResult<FacturaVentaBorradorResponse>(items.AsList(), pagina.Pagina, pagina.TamanoPagina, total));
    }
}
