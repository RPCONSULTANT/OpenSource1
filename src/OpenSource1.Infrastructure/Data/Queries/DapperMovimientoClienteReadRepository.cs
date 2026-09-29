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

    private static readonly ColumnasPermitidas ColumnasEstadoCuenta = new("Nombre", "Codigo", "Total");

    /// <summary>
    /// Detalle que cuenta A LA FECHA DE CORTE (Task 7.3), sobre <c>d</c> (detalle) y <c>a</c> (el movimiento contrario de una
    /// aplicación, <c>LEFT JOIN</c> por <c>MovimientoClienteAplicadoId</c>): fila con fecha ≤ corte y, si es una aplicación, con el
    /// movimiento contrario también registrado ≤ corte. Así una aplicación fechada antes que su pago no reduce la factura hasta
    /// que el pago existe, y las dos patas de cada aplicación entran o salen juntas (el total del cliente = Σ importes originales
    /// ≤ corte = saldo de CxC a esa fecha).
    /// </summary>
    private const string DetalleACorte =
        """d."FechaRegistro" <= @FechaCorte AND (a."Id" IS NULL OR a."FechaRegistro" <= @FechaCorte)""";

    /// <summary>
    /// Movimientos del socio con su restante derivado; el filtro de abiertos y el orden se aplican sobre esta consulta. Con
    /// fecha de corte, el restante es el de esa fecha (<see cref="DetalleACorte"/>) y solo entran los movimientos registrados
    /// hasta entonces; <paramref name="filtrosMovimiento"/> se añaden al WHERE de <c>MovimientosCliente</c>.
    /// </summary>
    private static string BaseSql(bool conCorte, string filtrosMovimiento = "")
    {
        var restante = conCorte
            ? $"""
                SELECT COALESCE(SUM(d."Importe"), 0) AS "Restante"
                FROM "MovimientosClienteDetalle" d
                LEFT JOIN "MovimientosCliente" a ON a."Id" = d."MovimientoClienteAplicadoId"
                WHERE d."MovimientoClienteId" = m."Id" AND {DetalleACorte}
              """
            : """
                SELECT COALESCE(SUM(d."Importe"), 0) AS "Restante"
                FROM "MovimientosClienteDetalle" d WHERE d."MovimientoClienteId" = m."Id"
              """;
        var corte = conCorte ? """ AND m."FechaRegistro" <= @FechaCorte""" : string.Empty;

        return $"""
            SELECT m."Id", m."SocioNegocioId", m."FechaRegistro", m."FechaDocumento", m."FechaVencimiento", m."TipoDocumento",
                   m."NumeroDocumento", m."Descripcion", m."ImporteOriginal", r."Restante" AS "ImporteRestante",
                   r."Restante" <> 0 AS "Abierta", m."GrupoClienteContableId", m."CuentaCxCId", c."Numero" AS "NumeroCuentaCxC",
                   m."TipoOrigen", m."ClaveOrigen", m."CreatedAtUtc", m."CreatedBy"
            FROM "MovimientosCliente" m
            CROSS JOIN LATERAL (
            {restante}
            ) r
            LEFT JOIN "CuentasContables" c ON c."Id" = m."CuentaCxCId"
            WHERE m."SocioNegocioId" = @SocioNegocioId{corte}{filtrosMovimiento}
            """;
    }

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

        // Tipo de documento y fecha de corte van DENTRO de la base (sobre MovimientosCliente, antes de calcular restantes).
        var filtrosMovimiento = string.Empty;
        if (search.TipoDocumento is { } tipoDocumento)
        {
            filtrosMovimiento = """ AND m."TipoDocumento" = @TipoDocumento""";
            parameters.Add("TipoDocumento", (short)tipoDocumento);
        }

        if (search.FechaCorte is { } fechaCorte)
        {
            parameters.Add("FechaCorte", fechaCorte);
        }

        var baseSql = BaseSql(search.FechaCorte is not null, filtrosMovimiento);

        var whereSql = filters.Count == 0 ? string.Empty : Environment.NewLine + "WHERE " + string.Join(" AND ", filters);

        // Por defecto, cronológico (FechaRegistro, Id): el orden natural de un estado de cuenta.
        var ordenColumna = ColumnasPermitidas.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "FechaRegistro";
        var direccionSql = pagina.Descendente ? "DESC" : "ASC";

        parameters.Add("TamanoPagina", pagina.TamanoPagina);
        parameters.Add("Offset", pagina.Offset);

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

    public async Task<IReadOnlyList<MovimientoClienteResponse>> ListAbiertosAsync(Guid socioNegocioId, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT * FROM ({BaseSql(conCorte: false)}) b
            WHERE "Abierta"
            ORDER BY "FechaRegistro", "Id"
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var items = await session.Connection.QueryAsync<MovimientoClienteResponse>(new CommandDefinition(
            sql, new { SocioNegocioId = socioNegocioId }, session.CurrentTransaction, cancellationToken: cancellationToken));
        return items.AsList();
    }

    public async Task<EstadoCuentaResponse> GetEstadoCuentaAsync(
        EstadoCuentaCriterios criterios, PageRequest paginacion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criterios);
        var fechaCorte = criterios.FechaCorte
            ?? throw new ArgumentException("La fecha de corte es obligatoria (el handler la fija).", nameof(criterios));
        var pagina = paginacion.Normalizar();

        var parameters = new DynamicParameters();
        parameters.Add("FechaCorte", fechaCorte);
        var filtrosMovimiento = new List<string> { "m.\"FechaRegistro\" <= @FechaCorte" };
        if (criterios.SocioNegocioId is { } socioId)
        {
            filtrosMovimiento.Add("m.\"SocioNegocioId\" = @SocioNegocioId");
            parameters.Add("SocioNegocioId", socioId);
        }

        if (!string.IsNullOrWhiteSpace(criterios.Texto))
        {
            // Por contenido en código o nombre comercial, ANTES de agregar (solo se agregan los movimientos de esos socios).
            filtrosMovimiento.Add("""
                m."SocioNegocioId" IN (SELECT st."Id" FROM "SociosNegocio" st
                                       WHERE st."Codigo" ILIKE @Texto ESCAPE '\' OR st."NombreComercial" ILIKE @Texto ESCAPE '\')
                """);
            parameters.Add("Texto", $"%{FilterExpressionBuilder.EscaparMetacaracteresLike(criterios.Texto.Trim())}%");
        }

        var exterior = criterios.SoloConSaldo ? "WHERE \"DocumentosAbiertos\" > 0" : string.Empty;

        // r: restante a la fecha de corte por movimiento; c: tramos por cliente (días vencidos = corte − vencimiento, en días);
        // f: con código y nombre del socio. Restantes positivos por tramo; negativos, "sin aplicar".
        var filasSql = $"""
            WITH r AS (
                SELECT m."SocioNegocioId", m."FechaVencimiento",
                       COALESCE(SUM(d."Importe") FILTER (WHERE {DetalleACorte}), 0) AS "Restante"
                FROM "MovimientosCliente" m
                LEFT JOIN "MovimientosClienteDetalle" d ON d."MovimientoClienteId" = m."Id"
                LEFT JOIN "MovimientosCliente" a ON a."Id" = d."MovimientoClienteAplicadoId"
                WHERE {string.Join(" AND ", filtrosMovimiento)}
                GROUP BY m."Id"
            ),
            c AS (
                SELECT r."SocioNegocioId",
                       COALESCE(SUM(r."Restante") FILTER (WHERE r."Restante" > 0 AND @FechaCorte - r."FechaVencimiento" <= 0), 0) AS "Corriente",
                       COALESCE(SUM(r."Restante") FILTER (WHERE r."Restante" > 0 AND @FechaCorte - r."FechaVencimiento" BETWEEN 1 AND 30), 0) AS "Dias1a30",
                       COALESCE(SUM(r."Restante") FILTER (WHERE r."Restante" > 0 AND @FechaCorte - r."FechaVencimiento" BETWEEN 31 AND 60), 0) AS "Dias31a60",
                       COALESCE(SUM(r."Restante") FILTER (WHERE r."Restante" > 0 AND @FechaCorte - r."FechaVencimiento" BETWEEN 61 AND 90), 0) AS "Dias61a90",
                       COALESCE(SUM(r."Restante") FILTER (WHERE r."Restante" > 0 AND @FechaCorte - r."FechaVencimiento" > 90), 0) AS "Mas90",
                       COALESCE(SUM(r."Restante") FILTER (WHERE r."Restante" < 0), 0) AS "SinAplicar",
                       SUM(r."Restante") AS "Total",
                       (COUNT(*) FILTER (WHERE r."Restante" <> 0))::int AS "DocumentosAbiertos"
                FROM r
                GROUP BY r."SocioNegocioId"
            ),
            f AS (
                SELECT c.*, s."Codigo", s."NombreComercial" AS "Nombre"
                FROM c JOIN "SociosNegocio" s ON s."Id" = c."SocioNegocioId"
            )
            """;

        // Por defecto, por nombre ascendente (el controlador fija descendente=false); desempate estable por el Id del socio.
        var ordenColumna = ColumnasEstadoCuenta.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "Nombre";
        var direccion = pagina.Descendente ? "DESC" : "ASC";
        parameters.Add("TamanoPagina", pagina.TamanoPagina);
        parameters.Add("Offset", pagina.Offset);

        // Una sola pasada por el libro: la página lleva en cada fila el número de clientes y los tramos sumados de TODOS los
        // filtrados (funciones de ventana sobre f, evaluadas antes del LIMIT). Solo si la página sale vacía (página posterior a
        // la última, o ningún cliente) hace falta la consulta de totales aparte.
        var pageSql = $"""
            {filasSql}
            SELECT f.*, COUNT(*) OVER () AS "TotCantidad",
                   SUM("Corriente") OVER () AS "TotCorriente", SUM("Dias1a30") OVER () AS "TotDias1a30",
                   SUM("Dias31a60") OVER () AS "TotDias31a60", SUM("Dias61a90") OVER () AS "TotDias61a90",
                   SUM("Mas90") OVER () AS "TotMas90", SUM("SinAplicar") OVER () AS "TotSinAplicar", SUM("Total") OVER () AS "TotTotal"
            FROM f {exterior}
            ORDER BY {ColumnasEstadoCuenta.Citar(ordenColumna)} {direccion}, "SocioNegocioId" {direccion}
            LIMIT @TamanoPagina OFFSET @Offset
            """;

        await session.EnsureOpenAsync(cancellationToken);
        var filas = (await session.Connection.QueryAsync<FilaEstadoCuenta>(
            new CommandDefinition(pageSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken))).AsList();

        TotalesEstadoCuenta totales;
        if (filas.Count > 0)
        {
            var f = filas[0];
            totales = new TotalesEstadoCuenta
            {
                Cantidad = f.TotCantidad, Corriente = f.TotCorriente, Dias1a30 = f.TotDias1a30, Dias31a60 = f.TotDias31a60,
                Dias61a90 = f.TotDias61a90, Mas90 = f.TotMas90, SinAplicar = f.TotSinAplicar, Total = f.TotTotal,
            };
        }
        else
        {
            var totalesSql = $"""
                {filasSql}
                SELECT COUNT(*) AS "Cantidad",
                       COALESCE(SUM("Corriente"), 0) AS "Corriente", COALESCE(SUM("Dias1a30"), 0) AS "Dias1a30",
                       COALESCE(SUM("Dias31a60"), 0) AS "Dias31a60", COALESCE(SUM("Dias61a90"), 0) AS "Dias61a90",
                       COALESCE(SUM("Mas90"), 0) AS "Mas90", COALESCE(SUM("SinAplicar"), 0) AS "SinAplicar",
                       COALESCE(SUM("Total"), 0) AS "Total"
                FROM f {exterior}
                """;
            totales = await session.Connection.QuerySingleAsync<TotalesEstadoCuenta>(
                new CommandDefinition(totalesSql, parameters, session.CurrentTransaction, cancellationToken: cancellationToken));
        }

        var items = filas.ConvertAll(f => new EstadoCuentaClienteResponse
        {
            SocioNegocioId = f.SocioNegocioId, Codigo = f.Codigo, Nombre = f.Nombre, Corriente = f.Corriente, Dias1a30 = f.Dias1a30,
            Dias31a60 = f.Dias31a60, Dias61a90 = f.Dias61a90, Mas90 = f.Mas90, SinAplicar = f.SinAplicar, Total = f.Total,
            DocumentosAbiertos = f.DocumentosAbiertos,
        });

        return new EstadoCuentaResponse(
            new PagedResult<EstadoCuentaClienteResponse>(items, pagina.Pagina, pagina.TamanoPagina, totales.Cantidad),
            fechaCorte,
            new EstadoCuentaTramos
            {
                Corriente = totales.Corriente, Dias1a30 = totales.Dias1a30, Dias31a60 = totales.Dias31a60,
                Dias61a90 = totales.Dias61a90, Mas90 = totales.Mas90, SinAplicar = totales.SinAplicar, Total = totales.Total,
            });
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

    /// <summary>Fila de la página del estado de cuenta con los totales de todos los clientes filtrados (ventana).</summary>
    private sealed class FilaEstadoCuenta
    {
        public Guid SocioNegocioId { get; init; }
        public string Codigo { get; init; } = string.Empty;
        public string Nombre { get; init; } = string.Empty;
        public decimal Corriente { get; init; }
        public decimal Dias1a30 { get; init; }
        public decimal Dias31a60 { get; init; }
        public decimal Dias61a90 { get; init; }
        public decimal Mas90 { get; init; }
        public decimal SinAplicar { get; init; }
        public decimal Total { get; init; }
        public int DocumentosAbiertos { get; init; }
        public long TotCantidad { get; init; }
        public decimal TotCorriente { get; init; }
        public decimal TotDias1a30 { get; init; }
        public decimal TotDias31a60 { get; init; }
        public decimal TotDias61a90 { get; init; }
        public decimal TotMas90 { get; init; }
        public decimal TotSinAplicar { get; init; }
        public decimal TotTotal { get; init; }
    }

    private sealed class TotalesEstadoCuenta
    {
        public long Cantidad { get; init; }
        public decimal Corriente { get; init; }
        public decimal Dias1a30 { get; init; }
        public decimal Dias31a60 { get; init; }
        public decimal Dias61a90 { get; init; }
        public decimal Mas90 { get; init; }
        public decimal SinAplicar { get; init; }
        public decimal Total { get; init; }
    }
}
