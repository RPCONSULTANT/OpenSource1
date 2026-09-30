using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Infrastructure.Data;

// Nota: los parámetros DateOnly (@Fecha) requieren DapperDateOnlyTypeHandler (ver DependencyInjection.AddApplicationData).
public sealed class GeneradorNumeroDocumento(IDbSession session) : IGeneradorNumeroDocumento
{
    private const string LineaVigenteSql = """
        SELECT "Id", "NumeroInicial", "NumeroFinal", "UltimoNumeroUsado", "Incremento", "NumeroAviso"
        FROM "LineasSerie"
        WHERE "SerieId" = @SerieId AND "FechaInicial" <= @Fecha AND "Bloqueada" = false AND "IsDeleted" = false
        ORDER BY "FechaInicial" DESC
        LIMIT 1
        """;

    public async Task<Result<NumeroGenerado>> SiguienteAsync(
        Guid serieId, TipoDocumentoSerie tipoEsperado, DateOnly fecha, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);
        if (session.CurrentTransaction is null)
        {
            return Result<NumeroGenerado>.Fallo(SinTransaccion());
        }

        // FOR SHARE: la serie no puede desactivarse ni cambiar de tipo hasta el commit/rollback de este documento.
        var serie = await LeerSerieAsync(serieId, "FOR SHARE", cancellationToken);
        if (Validar(serie, tipoEsperado) is { } error)
        {
            return Result<NumeroGenerado>.Fallo(error);
        }

        // FOR UPDATE: dos transacciones concurrentes sobre la misma serie se serializan aquí (sin huecos ni duplicados).
        var linea = await session.Connection.QuerySingleOrDefaultAsync<LineaFila?>(new CommandDefinition(
            LineaVigenteSql + " FOR UPDATE", new { SerieId = serieId, Fecha = fecha }, session.CurrentTransaction, cancellationToken: cancellationToken));
        if (linea is null)
        {
            return Result<NumeroGenerado>.Fallo(SinLineaVigente(serie!.Codigo, fecha));
        }

        var calculo = CalculoNumeroSerie.Siguiente(
            serie!.Codigo, linea.NumeroInicial, linea.NumeroFinal, linea.UltimoNumeroUsado, linea.Incremento, linea.NumeroAviso);
        if (!calculo.TryObtenerValor(out var numero))
        {
            return calculo;
        }

        await session.Connection.ExecuteAsync(new CommandDefinition(
            """UPDATE "LineasSerie" SET "UltimoNumeroUsado" = @Nuevo WHERE "Id" = @Id""",
            new { Nuevo = numero.Numero, linea.Id }, session.CurrentTransaction, cancellationToken: cancellationToken));
        return calculo;
    }

    public async Task<Result<NumeroGenerado>> SiguientePorTipoAsync(TipoDocumentoSerie tipo, DateOnly fecha, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);
        if (session.CurrentTransaction is null)
        {
            return Result<NumeroGenerado>.Fallo(SinTransaccion());
        }

        var configurada = await SerieConfiguradaAsync(tipo, cancellationToken);
        return configurada.TryObtenerValor(out var serieId)
            ? await SiguienteAsync(serieId, tipo, fecha, cancellationToken)
            : Result<NumeroGenerado>.Fallo(configurada);
    }

    public async Task<Result<NumeroGenerado>> ProximoNumeroAsync(Guid serieId, DateOnly fecha, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);
        var serie = await LeerSerieAsync(serieId, string.Empty, cancellationToken);
        if (serie is null)
        {
            return Result<NumeroGenerado>.Fallo(SerieInexistente());
        }

        if (!serie.Activa)
        {
            return Result<NumeroGenerado>.Fallo(Inactiva(serie.Codigo));
        }

        var linea = await session.Connection.QuerySingleOrDefaultAsync<LineaFila?>(new CommandDefinition(
            LineaVigenteSql, new { SerieId = serieId, Fecha = fecha }, session.CurrentTransaction, cancellationToken: cancellationToken));
        return linea is null
            ? Result<NumeroGenerado>.Fallo(SinLineaVigente(serie.Codigo, fecha))
            : CalculoNumeroSerie.Siguiente(serie.Codigo, linea.NumeroInicial, linea.NumeroFinal, linea.UltimoNumeroUsado, linea.Incremento, linea.NumeroAviso);
    }

    public async Task<Result<Guid>> SerieConfiguradaAsync(TipoDocumentoSerie tipo, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);
        var serieId = await session.Connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            """SELECT "SerieId" FROM "ConfiguracionesNumeracion" WHERE "TipoDocumento" = @Tipo AND "IsDeleted" = false""",
            new { Tipo = (short)tipo }, session.CurrentTransaction, cancellationToken: cancellationToken));
        return serieId is { } id
            ? Result<Guid>.Exito(id)
            : Result<Guid>.Fallo(new Error(
                "numeracion.sin_configuracion", $"No hay una serie configurada para {TipoDocumentoSerieNombres.Nombre(tipo)}.", "TipoDocumento"));
    }

    public async Task<Result> ValidarSerieAsync(Guid serieId, TipoDocumentoSerie tipoEsperado, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);
        var serie = await LeerSerieAsync(serieId, string.Empty, cancellationToken);
        return Validar(serie, tipoEsperado) is { } error ? Result.Fallo(error) : Result.Exito();
    }

    private Task<SerieFila?> LeerSerieAsync(Guid serieId, string bloqueo, CancellationToken cancellationToken) =>
        session.Connection.QuerySingleOrDefaultAsync<SerieFila?>(new CommandDefinition(
            $"""SELECT "Id", "Codigo", "TipoDocumento", "Activa" FROM "Series" WHERE "Id" = @Id AND "IsDeleted" = false {bloqueo}""",
            new { Id = serieId }, session.CurrentTransaction, cancellationToken: cancellationToken));

    private static Error? Validar(SerieFila? serie, TipoDocumentoSerie tipoEsperado)
    {
        if (serie is null)
        {
            return SerieInexistente();
        }

        if ((TipoDocumentoSerie)serie.TipoDocumento != tipoEsperado)
        {
            return new Error(
                "numeracion.tipo_incorrecto",
                $"La serie '{serie.Codigo}' numera {TipoDocumentoSerieNombres.Nombre((TipoDocumentoSerie)serie.TipoDocumento)}, " +
                $"no {TipoDocumentoSerieNombres.Nombre(tipoEsperado)}.",
                "SerieId");
        }

        return serie.Activa ? null : Inactiva(serie.Codigo);
    }

    private static Error SinTransaccion() => new(
        "numeracion.sin_transaccion", "La generación de números de documento requiere una transacción activa.");

    private static Error SerieInexistente() => new("numeracion.serie_inexistente", "No existe la serie de numeración indicada.", "SerieId");

    private static Error Inactiva(string codigo) => new("numeracion.serie_inactiva", $"La serie '{codigo}' está inactiva.", "SerieId");

    private static Error SinLineaVigente(string codigo, DateOnly fecha) => new(
        "numeracion.sin_linea_vigente", $"La serie '{codigo}' no tiene una línea vigente para la fecha {fecha:dd/MM/yyyy}.", "SerieId");

    private sealed record SerieFila(Guid Id, string Codigo, short TipoDocumento, bool Activa);

    private sealed record LineaFila(Guid Id, string NumeroInicial, string NumeroFinal, string UltimoNumeroUsado, int Incremento, string? NumeroAviso);
}
