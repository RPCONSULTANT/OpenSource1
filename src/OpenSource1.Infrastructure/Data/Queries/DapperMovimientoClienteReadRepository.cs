using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.MovimientosCliente;
using OpenSource1.Application.Features.MovimientosCliente.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data.Queries;

/// <summary>
/// Libro de clientes (Task 6.3). Nada derivado se almacena (D1, D7): <c>ImporteRestante = Σ detalle.Importe</c> del movimiento,
/// <c>Abierta = ImporteRestante &lt;&gt; 0</c> y <c>Saldo = Σ detalle.Importe</c> de todos los movimientos del socio (índices
/// <c>IX_MovimientosCliente_SocioNegocioId_FechaRegistro</c> e <c>IX_MovimientosClienteDetalle_MovimientoClienteId</c>).
/// </summary>
public sealed class DapperMovimientoClienteReadRepository(IDbSession session) : IMovimientoClienteReadRepository
{
    private static readonly ColumnasPermitidas ColumnasPermitidas =
        new("Id", "FechaRegistro", "FechaVencimiento", "NumeroDocumento", "ImporteOriginal", "ImporteRestante");

    public async Task<bool> ExisteSocioAsync(Guid socioNegocioId, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);
        return await session.Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """SELECT EXISTS (SELECT 1 FROM "SociosNegocio" WHERE "Id" = @Id AND "IsDeleted" = false)""",
            new { Id = socioNegocioId }, session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<MovimientoClienteResponse>> ListAsync(
        MovimientoClienteSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);
        var pagina = paginacion.Normalizar();

        var filters = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("SocioNegocioId", search.SocioNegocioId);

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

        if (search.SoloAbiertos is { } soloAbiertos)
        {
            filters.Add("\"Abierta\" = @Abierta");
            parameters.Add("Abierta", soloAbiertos);
        }

        var whereSql = filters.Count == 0 ? string.Empty : Environment.NewLine + "WHERE " + string.Join(" AND ", filters);

        // Por defecto, cronológico (FechaRegistro, Id): el orden natural de un estado de cuenta.
        var ordenColumna = ColumnasPermitidas.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "FechaRegistro";
        var direccionSql = pagina.Descendente ? "DESC" : "ASC";

        parameters.Add("TamanoPagina", pagina.TamanoPagina);
        parameters.Add("Offset", pagina.Offset);

        const string baseSql = """
            SELECT m."Id", m."SocioNegocioId", m."FechaRegistro", m."FechaDocumento", m."FechaVencimiento", m."TipoDocumento",
                   m."NumeroDocumento", m."Descripcion", m."ImporteOriginal", r."Restante" AS "ImporteRestante",
                   r."Restante" <> 0 AS "Abierta", m."GrupoClienteContableId", m."CuentaCxCId", c."Numero" AS "NumeroCuentaCxC",
                   m."TipoOrigen", m."ClaveOrigen", m."CreatedAtUtc", m."CreatedBy"
            FROM "MovimientosCliente" m
            CROSS JOIN LATERAL (
                SELECT COALESCE(SUM(d."Importe"), 0) AS "Restante"
                FROM "MovimientosClienteDetalle" d WHERE d."MovimientoClienteId" = m."Id"
            ) r
            LEFT JOIN "CuentasContables" c ON c."Id" = m."CuentaCxCId"
            WHERE m."SocioNegocioId" = @SocioNegocioId
            """;

        var countSql = $"""
            SELECT COUNT(*) FROM ({baseSql}) b
            {whereSql}
            """;

        var pageSql = $"""
            SELECT * FROM ({baseSql}) b
            {whereSql}
            ORDER BY {ColumnasPermitidas.Citar(ordenColumna)} {direccionSql}, "Id" {direccionSql}
            LIMIT @TamanoPagina OFFSET @Offset
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var total = await session.Connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));
        var items = await session.Connection.QueryAsync<MovimientoClienteResponse>(
            new CommandDefinition(pageSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        return new PagedResult<MovimientoClienteResponse>(items.AsList(), pagina.Pagina, pagina.TamanoPagina, total);
    }

    public async Task<SaldoClienteResponse> GetSaldoAsync(Guid socioNegocioId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COALESCE(SUM(r."Restante"), 0) AS "Saldo", COUNT(*) FILTER (WHERE r."Restante" <> 0)::int AS "Abiertos"
            FROM (
                SELECT COALESCE(SUM(d."Importe"), 0) AS "Restante"
                FROM "MovimientosCliente" m
                LEFT JOIN "MovimientosClienteDetalle" d ON d."MovimientoClienteId" = m."Id"
                WHERE m."SocioNegocioId" = @SocioNegocioId
                GROUP BY m."Id"
            ) r
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var (saldo, abiertos) = await session.Connection.QuerySingleAsync<(decimal, int)>(new CommandDefinition(
            sql, new { SocioNegocioId = socioNegocioId }, session.CurrentTransaction, cancellationToken: cancellationToken));
        return new SaldoClienteResponse(socioNegocioId, saldo, abiertos);
    }
}
