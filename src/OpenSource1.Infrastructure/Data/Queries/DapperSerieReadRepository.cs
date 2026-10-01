using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.Series;
using OpenSource1.Application.Features.Series.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Infrastructure.Data.Queries;

public sealed class DapperSerieReadRepository(IDbSession session) : ISerieReadRepository
{
    private static readonly ColumnasPermitidas ColumnasPermitidas = new("Codigo", "Descripcion", "TipoDocumento", "CreatedAtUtc");

    // Una sola definición de "Asignada" y "Referenciada" (alias s con la columna "Id") para el listado, la ficha y las guardas (UsoAsync).
    private const string AsignadaSql = """EXISTS (SELECT 1 FROM "ConfiguracionesNumeracion" c WHERE c."SerieId" = s."Id" AND c."IsDeleted" = false)""";

    private const string ReferenciadaSql = """
        (EXISTS (SELECT 1 FROM "PlantillasDiario" p WHERE p."SerieId" = s."Id" AND p."IsDeleted" = false)
         OR EXISTS (SELECT 1 FROM "LotesDiario" l WHERE l."SerieId" = s."Id" AND l."IsDeleted" = false))
        """;

    // "Usada" NO se calcula en SQL: lo decide CalculoNumeroSerie.EstaUsada en C# (ver SeriesUsadasAsync).
    private const string Base = $$"""
        SELECT s."Id", s."Codigo", s."Descripcion", s."TipoDocumento", s."PermiteHuecos", s."Activa", s."CreatedAtUtc",
               s.xmin::text::bigint AS "Xmin",
               v."NumeroInicial" AS "VigenteInicial", v."NumeroFinal" AS "VigenteFinal", v."UltimoNumeroUsado" AS "VigenteUltimo",
               v."Incremento" AS "VigenteIncremento", v."NumeroAviso" AS "VigenteAviso",
               {{AsignadaSql}} AS "Asignada",
               {{ReferenciadaSql}} AS "Referenciada"
        FROM "Series" s
        LEFT JOIN LATERAL (
            SELECT l."NumeroInicial", l."NumeroFinal", l."UltimoNumeroUsado", l."Incremento", l."NumeroAviso"
            FROM "LineasSerie" l
            WHERE l."SerieId" = s."Id" AND l."FechaInicial" <= @Hoy AND l."Bloqueada" = false AND l."IsDeleted" = false
            ORDER BY l."FechaInicial" DESC LIMIT 1) v ON true
        WHERE s."IsDeleted" = false
        """;

    public async Task<SerieDetalleResponse?> GetByIdAsync(Guid id, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);
        var fila = await session.Connection.QuerySingleOrDefaultAsync<SerieFila>(new CommandDefinition(
            $"""SELECT * FROM ({Base}) b WHERE "Id" = @Id""", new { Id = id, Hoy = hoy }, session.CurrentTransaction, cancellationToken: cancellationToken));
        if (fila is null)
        {
            return null;
        }

        var lineas = await session.Connection.QueryAsync<LineaFila>(new CommandDefinition(
            """
            SELECT "Id", "SerieId", "NumeroInicial", "NumeroFinal", "NumeroAviso", "UltimoNumeroUsado", "FechaInicial", "Incremento",
                   "Bloqueada", xmin::text::bigint AS "Xmin"
            FROM "LineasSerie" WHERE "SerieId" = @Id AND "IsDeleted" = false ORDER BY "FechaInicial"
            """,
            new { Id = id }, session.CurrentTransaction, cancellationToken: cancellationToken));

        var usada = lineas.Any(l => CalculoNumeroSerie.EstaUsada(l.NumeroInicial, l.UltimoNumeroUsado));
        return new SerieDetalleResponse(Mapear(fila, usada), [.. lineas.Select(l => new LineaSerieResponse
        {
            Id = l.Id,
            SerieId = l.SerieId,
            NumeroInicial = l.NumeroInicial,
            NumeroFinal = l.NumeroFinal,
            NumeroAviso = l.NumeroAviso,
            UltimoNumeroUsado = l.UltimoNumeroUsado,
            FechaInicial = l.FechaInicial,
            Incremento = l.Incremento,
            Bloqueada = l.Bloqueada,
            Usada = CalculoNumeroSerie.EstaUsada(l.NumeroInicial, l.UltimoNumeroUsado),
            Xmin = l.Xmin,
        })]);
    }

    public async Task<Result<PagedResult<SerieResponse>>> ListAsync(
        SerieSearchCriteria search, PageRequest paginacion, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        var pagina = paginacion.Normalizar();
        var filters = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("Hoy", hoy);
        FilterExpressionBuilder.AddTextFilter(filters, parameters, ColumnasPermitidas, "Codigo", search.Codigo);
        if (search.Tipo is { } tipo)
        {
            filters.Add("\"TipoDocumento\" = @Tipo");
            parameters.Add("Tipo", (short)tipo);
        }

        if (search.Activa is { } activa)
        {
            filters.Add("\"Activa\" = @Activa");
            parameters.Add("Activa", activa);
        }

        var whereSql = filters.Count == 0 ? string.Empty : Environment.NewLine + "WHERE " + string.Join(" AND ", filters);
        var orden = ColumnasPermitidas.Citar(ColumnasPermitidas.EsValida(pagina.OrdenarPor) ? pagina.OrdenarPor! : "Codigo");
        parameters.Add("TamanoPagina", pagina.TamanoPagina);
        parameters.Add("Offset", pagina.Offset);

        await session.EnsureOpenAsync(cancellationToken);
        var total = await session.Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            $"SELECT COUNT(*) FROM ({Base}) b{whereSql}", parameters, session.CurrentTransaction, cancellationToken: cancellationToken));
        var filas = await session.Connection.QueryAsync<SerieFila>(new CommandDefinition(
            $"""
            SELECT * FROM ({Base}) b{whereSql}
            ORDER BY {orden} {(pagina.Descendente ? "DESC" : "ASC")}, "Id" ASC
            LIMIT @TamanoPagina OFFSET @Offset
            """,
            parameters, session.CurrentTransaction, cancellationToken: cancellationToken));

        var lista = filas.AsList();
        var usadas = await SeriesUsadasAsync([.. lista.Select(f => f.Id)], cancellationToken);
        return Result<PagedResult<SerieResponse>>.Exito(
            new PagedResult<SerieResponse>([.. lista.Select(f => Mapear(f, usadas.Contains(f.Id)))], pagina.Pagina, pagina.TamanoPagina, total));
    }

    public async Task<UsoSerie> UsoAsync(Guid serieId, CancellationToken cancellationToken = default)
    {
        var usada = (await SeriesUsadasAsync([serieId], cancellationToken)).Count > 0;
        var protecciones = await session.Connection.QuerySingleAsync<(bool Asignada, bool Referenciada)>(new CommandDefinition(
            $"""
            SELECT {AsignadaSql} AS "Asignada", {ReferenciadaSql} AS "Referenciada"
            FROM (SELECT @Id AS "Id") s
            """,
            new { Id = serieId }, session.CurrentTransaction, cancellationToken: cancellationToken));
        return new UsoSerie(usada, protecciones.Asignada, protecciones.Referenciada);
    }

    /// <summary>
    /// Series (de <paramref name="serieIds"/>) con alguna línea viva USADA. "Usada" se decide SOLO en C# con
    /// <see cref="CalculoNumeroSerie.EstaUsada"/> (falla cerrado: un contador que no se interpreta, con otro prefijo o más ancho que
    /// la línea cuenta como usado), la misma definición que el flag de cada línea y que el borrado de una línea.
    /// </summary>
    private async Task<HashSet<Guid>> SeriesUsadasAsync(Guid[] serieIds, CancellationToken cancellationToken)
    {
        if (serieIds.Length == 0)
        {
            return [];
        }

        await session.EnsureOpenAsync(cancellationToken);
        var contadores = await session.Connection.QueryAsync<(Guid SerieId, string NumeroInicial, string UltimoNumeroUsado)>(new CommandDefinition(
            """
            SELECT "SerieId", "NumeroInicial", "UltimoNumeroUsado"
            FROM "LineasSerie" WHERE "SerieId" = ANY(@Ids) AND "IsDeleted" = false AND "UltimoNumeroUsado" <> ''
            """,
            new { Ids = serieIds }, session.CurrentTransaction, cancellationToken: cancellationToken));
        return [.. contadores.Where(c => CalculoNumeroSerie.EstaUsada(c.NumeroInicial, c.UltimoNumeroUsado)).Select(c => c.SerieId)];
    }

    public async Task<IReadOnlyList<LineaDeTipo>> LineasDelTipoAsync(TipoDocumentoSerie tipo, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);
        return (await session.Connection.QueryAsync<LineaDeTipo>(new CommandDefinition(
            """
            SELECT l."Id" AS "LineaId", l."SerieId", s."Codigo" AS "SerieCodigo", l."NumeroInicial", l."NumeroFinal"
            FROM "LineasSerie" l JOIN "Series" s ON s."Id" = l."SerieId"
            WHERE s."TipoDocumento" = @Tipo AND s."IsDeleted" = false AND l."IsDeleted" = false
            """,
            new { Tipo = (short)tipo }, session.CurrentTransaction, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<TipoDocumentoSerie?> BloquearSerieAsync(Guid serieId, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);
        if (session.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Bloquear una serie requiere una transacción activa.");
        }

        var tipo = await session.Connection.QuerySingleOrDefaultAsync<short?>(new CommandDefinition(
            """SELECT "TipoDocumento" FROM "Series" WHERE "Id" = @Id AND "IsDeleted" = false FOR UPDATE""",
            new { Id = serieId }, session.CurrentTransaction, cancellationToken: cancellationToken));
        return tipo is { } valor ? (TipoDocumentoSerie)valor : null;
    }

    public async Task<EstadoSerie?> LeerSerieCompartidaAsync(Guid serieId, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);
        if (session.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Leer una serie con bloqueo compartido requiere una transacción activa.");
        }

        var fila = await session.Connection.QuerySingleOrDefaultAsync<(short Tipo, bool Activa)?>(new CommandDefinition(
            """SELECT "TipoDocumento" AS "Tipo", "Activa" FROM "Series" WHERE "Id" = @Id AND "IsDeleted" = false FOR SHARE""",
            new { Id = serieId }, session.CurrentTransaction, cancellationToken: cancellationToken));
        return fila is { } f ? new EstadoSerie((TipoDocumentoSerie)f.Tipo, f.Activa) : null;
    }

    public async Task BloquearTipoAsync(TipoDocumentoSerie tipo, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);
        if (session.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Bloquear un tipo de serie requiere una transacción activa.");
        }

        await session.Connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(hashtextextended('series-tipo:' || @Tipo::text, 0))",
            new { Tipo = (int)tipo }, session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    public async Task BloquearCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);
        if (session.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Bloquear un código de serie requiere una transacción activa.");
        }

        await session.Connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(hashtextextended('series-codigo:' || @Codigo, 0))",
            new { Codigo = codigo }, session.CurrentTransaction, cancellationToken: cancellationToken));
    }

    private static SerieResponse Mapear(SerieFila f, bool usada)
    {
        string? proximo = null;
        string? aviso = null;
        if (f.VigenteInicial is null)
        {
            aviso = "Sin línea vigente hoy.";
        }
        else
        {
            var calculo = CalculoNumeroSerie.Siguiente(f.Codigo, f.VigenteInicial, f.VigenteFinal!, f.VigenteUltimo, f.VigenteIncremento ?? 1, f.VigenteAviso);
            if (calculo.TryObtenerValor(out var numero))
            {
                (proximo, aviso) = (numero.Numero, numero.Aviso);
            }
            else
            {
                aviso = calculo.Errores[0].Mensaje;
            }
        }

        return new SerieResponse
        {
            Id = f.Id,
            Codigo = f.Codigo,
            Descripcion = f.Descripcion,
            TipoDocumento = (TipoDocumentoSerie)f.TipoDocumento,
            PermiteHuecos = f.PermiteHuecos,
            Activa = f.Activa,
            UltimoNumeroUsado = f.VigenteInicial is not null && CalculoNumeroSerie.EstaUsada(f.VigenteInicial, f.VigenteUltimo) ? f.VigenteUltimo : null,
            ProximoNumero = proximo,
            Aviso = aviso,
            EnAviso = aviso is not null && proximo is not null,
            Usada = usada,
            Asignada = f.Asignada,
            Referenciada = f.Referenciada,
            Xmin = f.Xmin,
        };
    }

    private sealed class SerieFila
    {
        public Guid Id { get; init; }
        public string Codigo { get; init; } = string.Empty;
        public string Descripcion { get; init; } = string.Empty;
        public short TipoDocumento { get; init; }
        public bool PermiteHuecos { get; init; }
        public bool Activa { get; init; }
        public DateTimeOffset CreatedAtUtc { get; init; }
        public long Xmin { get; init; }
        public string? VigenteInicial { get; init; }
        public string? VigenteFinal { get; init; }
        public string? VigenteUltimo { get; init; }
        public int? VigenteIncremento { get; init; }
        public string? VigenteAviso { get; init; }
        public bool Asignada { get; init; }
        public bool Referenciada { get; init; }
    }

    private sealed class LineaFila
    {
        public Guid Id { get; init; }
        public Guid SerieId { get; init; }
        public string NumeroInicial { get; init; } = string.Empty;
        public string NumeroFinal { get; init; } = string.Empty;
        public string? NumeroAviso { get; init; }
        public string UltimoNumeroUsado { get; init; } = string.Empty;
        public DateOnly FechaInicial { get; init; }
        public int Incremento { get; init; }
        public bool Bloqueada { get; init; }
        public long Xmin { get; init; }
    }
}
