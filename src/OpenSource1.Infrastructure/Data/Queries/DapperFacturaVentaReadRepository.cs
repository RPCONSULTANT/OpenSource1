using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.FacturasVenta.Posteadas;
using OpenSource1.Application.Features.FacturasVenta.Posteadas.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

/// <summary>
/// Facturas de venta posteadas (Task 6.3). Sin filtro de borrado lógico en ningún JOIN: el documento es historia y muestra los
/// códigos actuales aunque el maestro se haya borrado después. La cabecera se aplana en una subconsulta (<c>b</c>) para filtrar
/// y ordenar por columnas sin calificar (mismo patrón que <see cref="DapperFacturaVentaBorradorReadRepository"/>); orden por
/// allow-list con desempate estable por la PK <c>Numero</c>.
/// </summary>
public sealed class DapperFacturaVentaReadRepository(IDbSession session) : IFacturaVentaReadRepository
{
    private static readonly ColumnasPermitidas ColumnasPermitidas =
        new("Numero", "NombreFacturacion", "FechaRegistro", "FechaDocumento", "ImporteTotal", "CreatedAtUtc");

    private const string Base = """
        SELECT f."Numero", f."NumeroBorrador",
               (SELECT fb."Id" FROM "FacturasVentaBorrador" fb
                WHERE fb."FacturaVentaNumero" = f."Numero" AND fb."IsDeleted" = false LIMIT 1) AS "FacturaVentaBorradorId",
               f."SocioNegocioId", sv."Codigo" AS "SocioNegocioCodigo",
               sv."NombreComercial" AS "SocioNegocioNombre", f."SocioNegocioFacturarAId", sf."Codigo" AS "SocioNegocioFacturarACodigo",
               f."NombreFacturacion", f."RazonSocialFacturacion", f."TipoDocumentoFiscal", f."NumeroDocumentoFiscal",
               f."DireccionFacturacionLinea1", f."DireccionFacturacionLinea2", f."CiudadFacturacion", f."PaisCodigoFacturacion",
               f."FechaRegistro", f."FechaDocumento", f."FechaVencimiento", f."TerminoPagoId", t."Codigo" AS "TerminoPagoCodigo",
               f."GrupoNegocioId", f."GrupoIvaNegocioId", f."GrupoClienteContableId", f."AlmacenId", a."Codigo" AS "AlmacenCodigo",
               f."Moneda", f."Descripcion", f."ImporteSinIva", f."ImporteIva", f."ImporteTotal",
               f."RegistroContableId", r."NumeroRegistro" AS "NumeroRegistroContable",
               (SELECT COUNT(*) FROM "LineasFacturaVenta" l WHERE l."FacturaVentaNumero" = f."Numero")::int AS "NumeroLineas",
               f."CreatedAtUtc", f."CreatedBy"
        FROM "FacturasVenta" f
        LEFT JOIN "SociosNegocio" sv ON sv."Id" = f."SocioNegocioId"
        LEFT JOIN "SociosNegocio" sf ON sf."Id" = f."SocioNegocioFacturarAId"
        LEFT JOIN "TerminosPago" t ON t."Id" = f."TerminoPagoId"
        LEFT JOIN "Almacenes" a ON a."Id" = f."AlmacenId"
        LEFT JOIN "RegistrosContables" r ON r."Id" = f."RegistroContableId"
        """;

    public async Task<FacturaVentaDetalleResponse?> GetByNumeroAsync(string numero, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(numero);

        var cabeceraSql = $"""
            SELECT * FROM ({Base}) b
            WHERE "Numero" = @Numero
            """;

        const string lineasSql = """
            SELECT l."Id", l."FacturaVentaNumero", l."NumeroLinea", l."Tipo", l."ProductoId", p."Codigo" AS "ProductoCodigo",
                   l."CuentaContableId", c."Numero" AS "CuentaContableNumero", l."Descripcion", l."AlmacenId", a."Codigo" AS "AlmacenCodigo",
                   l."UnidadMedidaId", u."Codigo" AS "UnidadMedidaCodigo", l."CantidadPorUnidadMedida", l."Cantidad", l."PrecioUnitario",
                   l."PorcentajeDescuentoLinea", l."ImporteDescuentoLinea", l."ImporteLinea", l."GrupoProductoId", l."GrupoIvaProductoId",
                   l."GrupoInventarioId", l."IdentificadorIva", l."PorcentajeIva", l."MovimientoProductoId"
            FROM "LineasFacturaVenta" l
            LEFT JOIN "Productos" p ON p."Id" = l."ProductoId"
            LEFT JOIN "CuentasContables" c ON c."Id" = l."CuentaContableId"
            LEFT JOIN "Almacenes" a ON a."Id" = l."AlmacenId"
            LEFT JOIN "UnidadesMedida" u ON u."Id" = l."UnidadMedidaId"
            WHERE l."FacturaVentaNumero" = @Numero
            ORDER BY l."NumeroLinea"
            """;

        const string ivaSql = """
            SELECT i."Id", i."IdentificadorIva", i."PorcentajeIva", i."BaseImponible", i."ImporteIva", i."CuentaIvaId",
                   c."Numero" AS "CuentaIvaNumero"
            FROM "LineasIvaFacturaVenta" i
            LEFT JOIN "CuentasContables" c ON c."Id" = i."CuentaIvaId"
            WHERE i."FacturaVentaNumero" = @Numero
            ORDER BY i."IdentificadorIva" COLLATE "C", i."Id"
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var parametros = new { Numero = numero };
        var cabecera = await session.Connection.QuerySingleOrDefaultAsync<FacturaVentaResponse>(
            new CommandDefinition(cabeceraSql, parametros, session.CurrentTransaction, cancellationToken: cancellationToken));
        if (cabecera is null)
        {
            return null;
        }

        var lineas = await session.Connection.QueryAsync<LineaFacturaVentaResponse>(
            new CommandDefinition(lineasSql, parametros, session.CurrentTransaction, cancellationToken: cancellationToken));
        var lineasIva = await session.Connection.QueryAsync<LineaIvaFacturaVentaResponse>(
            new CommandDefinition(ivaSql, parametros, session.CurrentTransaction, cancellationToken: cancellationToken));

        return new FacturaVentaDetalleResponse(cabecera, lineas.AsList(), lineasIva.AsList());
    }

    public async Task<PagedResult<FacturaVentaResponse>> ListAsync(
        FacturaVentaSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);
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

        if (search.Desde is { } desde)
        {
            filters.Add("\"FechaRegistro\" >= @Desde");
            parameters.Add("Desde", desde);
        }

        if (search.Hasta is { } hasta)
        {
            filters.Add("\"FechaRegistro\" <= @Hasta");
            parameters.Add("Hasta", hasta);
        }

        var whereSql = filters.Count == 0 ? string.Empty : Environment.NewLine + "WHERE " + string.Join(" AND ", filters);

        // Por defecto, por número (la serie FV es consecutiva: el orden de posteo).
        var ordenColumna = ColumnasPermitidas.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "Numero";
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
            ORDER BY {ColumnasPermitidas.Citar(ordenColumna)} {direccionSql}, "Numero" {direccionSql}
            LIMIT @TamanoPagina OFFSET @Offset
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var total = await session.Connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));
        var items = await session.Connection.QueryAsync<FacturaVentaResponse>(
            new CommandDefinition(pageSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        return new PagedResult<FacturaVentaResponse>(items.AsList(), pagina.Pagina, pagina.TamanoPagina, total);
    }
}
