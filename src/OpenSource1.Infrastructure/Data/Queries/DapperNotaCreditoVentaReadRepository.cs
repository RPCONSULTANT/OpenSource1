using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.NotasCreditoVenta.Posteadas;
using OpenSource1.Application.Features.NotasCreditoVenta.Posteadas.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

/// <summary>
/// Notas de crédito posteadas (Task 8.6). Mismo patrón que <see cref="DapperFacturaVentaReadRepository"/>: sin filtro de borrado
/// lógico en los JOIN (el documento es historia), subconsulta aplanada y orden por allow-list con desempate por la PK.
/// </summary>
public sealed class DapperNotaCreditoVentaReadRepository(IDbSession session) : INotaCreditoVentaReadRepository
{
    private static readonly ColumnasPermitidas ColumnasPermitidas =
        new("Numero", "FacturaVentaNumero", "NombreFacturacion", "FechaRegistro", "FechaDocumento", "ImporteTotal", "CreatedAtUtc");

    private const string Base = """
        SELECT n."Numero", n."NumeroBorrador", nb."Id" AS "NotaCreditoVentaBorradorId", n."FacturaVentaNumero", n."SocioNegocioId", sv."Codigo" AS "SocioNegocioCodigo",
               sv."NombreComercial" AS "SocioNegocioNombre", n."SocioNegocioFacturarAId", sf."Codigo" AS "SocioNegocioFacturarACodigo",
               n."NombreFacturacion", n."RazonSocialFacturacion", n."TipoDocumentoFiscal", n."NumeroDocumentoFiscal",
               n."FechaRegistro", n."FechaDocumento", n."GrupoNegocioId", n."GrupoIvaNegocioId", n."GrupoClienteContableId",
               n."CuentaCxCId", n."Moneda", n."Descripcion", n."ImporteSinIva", n."ImporteIva", n."ImporteTotal",
               n."RegistroContableId", r."NumeroRegistro" AS "NumeroRegistroContable",
               (SELECT COUNT(*) FROM "LineasNotaCreditoVenta" l WHERE l."NotaCreditoVentaNumero" = n."Numero")::int AS "NumeroLineas",
               n."CreatedAtUtc", n."CreatedBy"
        FROM "NotasCreditoVenta" n
        LEFT JOIN "SociosNegocio" sv ON sv."Id" = n."SocioNegocioId"
        LEFT JOIN "SociosNegocio" sf ON sf."Id" = n."SocioNegocioFacturarAId"
        LEFT JOIN "RegistrosContables" r ON r."Id" = n."RegistroContableId"
        -- A lo sumo un borrador vivo enlaza cada nota (índice único parcial): el JOIN no duplica filas.
        LEFT JOIN "NotasCreditoVentaBorrador" nb ON nb."NotaCreditoVentaNumero" = n."Numero" AND nb."IsDeleted" = false
        """;

    public async Task<NotaCreditoVentaDetalleResponse?> GetByNumeroAsync(string numero, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(numero);
        await session.EnsureOpenAsync(cancellationToken);
        var parametros = new { Numero = numero };
        var cabecera = await session.Connection.QuerySingleOrDefaultAsync<NotaCreditoVentaResponse>(new CommandDefinition(
            $"""SELECT * FROM ({Base}) b WHERE "Numero" = @Numero""", parametros, session.CurrentTransaction, cancellationToken: cancellationToken));
        if (cabecera is null)
        {
            return null;
        }

        var lineas = await session.Connection.QueryAsync<LineaNotaCreditoVentaResponse>(new CommandDefinition(
            """
            SELECT l."Id", l."NotaCreditoVentaNumero", l."NumeroLinea", l."LineaFacturaVentaId", l."Tipo", l."ProductoId",
                   p."Codigo" AS "ProductoCodigo", l."CuentaContableId", c."Numero" AS "CuentaContableNumero", l."Descripcion",
                   l."AlmacenId", a."Codigo" AS "AlmacenCodigo", l."UnidadMedidaId", u."Codigo" AS "UnidadMedidaCodigo",
                   l."CantidadPorUnidadMedida", l."Cantidad", l."PrecioUnitario", l."PorcentajeDescuentoLinea", l."ImporteDescuentoLinea",
                   l."ImporteLinea", l."GrupoProductoId", l."GrupoIvaProductoId", l."GrupoInventarioId", l."IdentificadorIva",
                   l."PorcentajeIva", l."DevolverInventario", l."MovimientoProductoId"
            FROM "LineasNotaCreditoVenta" l
            LEFT JOIN "Productos" p ON p."Id" = l."ProductoId"
            LEFT JOIN "CuentasContables" c ON c."Id" = l."CuentaContableId"
            LEFT JOIN "Almacenes" a ON a."Id" = l."AlmacenId"
            LEFT JOIN "UnidadesMedida" u ON u."Id" = l."UnidadMedidaId"
            WHERE l."NotaCreditoVentaNumero" = @Numero
            ORDER BY l."NumeroLinea"
            """,
            parametros, session.CurrentTransaction, cancellationToken: cancellationToken));
        var lineasIva = await session.Connection.QueryAsync<LineaIvaNotaCreditoVentaResponse>(new CommandDefinition(
            """
            SELECT i."Id", i."IdentificadorIva", i."PorcentajeIva", i."BaseImponible", i."ImporteIva", i."CuentaIvaId",
                   c."Numero" AS "CuentaIvaNumero"
            FROM "LineasIvaNotaCreditoVenta" i
            LEFT JOIN "CuentasContables" c ON c."Id" = i."CuentaIvaId"
            WHERE i."NotaCreditoVentaNumero" = @Numero
            ORDER BY i."IdentificadorIva" COLLATE "C", i."Id"
            """,
            parametros, session.CurrentTransaction, cancellationToken: cancellationToken));

        return new NotaCreditoVentaDetalleResponse(cabecera, lineas.AsList(), lineasIva.AsList());
    }

    public async Task<PagedResult<NotaCreditoVentaResponse>> ListAsync(
        NotaCreditoVentaSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);
        var pagina = paginacion.Normalizar();
        var filters = new List<string>();
        var parameters = new DynamicParameters();

        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Numero", search.Numero);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "FacturaVentaNumero", search.FacturaVentaNumero);
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
        var ordenColumna = ColumnasPermitidas.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "Numero";
        var direccionSql = pagina.Descendente ? "DESC" : "ASC";
        parameters.Add("TamanoPagina", pagina.TamanoPagina);
        parameters.Add("Offset", pagina.Offset);

        await session.EnsureOpenAsync(cancellationToken);
        var total = await session.Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            $"SELECT COUNT(*) FROM ({Base}) b {whereSql}", parameters, session.CurrentTransaction, cancellationToken: cancellationToken));
        var items = await session.Connection.QueryAsync<NotaCreditoVentaResponse>(new CommandDefinition(
            $"""
            SELECT * FROM ({Base}) b
            {whereSql}
            ORDER BY {ColumnasPermitidas.Citar(ordenColumna)} {direccionSql}, "Numero" {direccionSql}
            LIMIT @TamanoPagina OFFSET @Offset
            """,
            parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        return new PagedResult<NotaCreditoVentaResponse>(items.AsList(), pagina.Pagina, pagina.TamanoPagina, total);
    }
}
