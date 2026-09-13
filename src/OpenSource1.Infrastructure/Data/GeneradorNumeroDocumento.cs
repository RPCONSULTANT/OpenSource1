using System.Data;
using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Core.Common;

namespace OpenSource1.Infrastructure.Data;

public sealed class GeneradorNumeroDocumento(IDbSession session) : IGeneradorNumeroDocumento
{
    // Dapper (a diferencia del proveedor Npgsql de EF Core, que mapea DateOnly a "date" de forma
    // nativa) no sabe inferir el DbType de un parámetro DateOnly: SqlMapper.LookupDbType lanza
    // NotSupportedException al primer intento de ejecutar la consulta con "Fecha" en los
    // parámetros. Verificado empíricamente al correr el test de concurrencia contra Postgres real
    // (ver GeneradorNumeroDocumentoTests). Se registra un TypeHandler una sola vez, a nivel de
    // proceso, en el constructor estático.
    static GeneradorNumeroDocumento()
    {
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
    }

    public async Task<Result<string>> SiguienteAsync(string codigoSerie, DateOnly fecha, CancellationToken cancellationToken = default)
    {
        await session.EnsureOpenAsync(cancellationToken);

        // Requiere una transacción activa: FOR UPDATE sin transacción no retiene el bloqueo
        // más allá del statement. El llamante (un handler de posteo en Fases 4/6) debe estar
        // dentro de un IUnitOfWork.BeginTransactionAsync.
        if (session.CurrentTransaction is null)
        {
            return Result<string>.Fallo(new Error(
                "numeracion.sin_transaccion",
                "La generación de números de documento requiere una transacción activa."));
        }

        const string selectSerieSql = """
            SELECT "Id", "PermiteHuecos" FROM "Series"
            WHERE "Codigo" = @Codigo AND "IsDeleted" = false
            """;
        var serie = await session.Connection.QuerySingleOrDefaultAsync<(Guid Id, bool PermiteHuecos)?>(
            new CommandDefinition(selectSerieSql, new { Codigo = codigoSerie }, session.CurrentTransaction, cancellationToken: cancellationToken));

        if (serie is null)
        {
            return Result<string>.Fallo(new Error("numeracion.serie_inexistente", $"No existe la serie '{codigoSerie}'.", nameof(codigoSerie)));
        }

        // FOR UPDATE reserva la fila hasta el commit/rollback de la transacción actual.
        // Sin PermiteHuecos, dos transacciones concurrentes sobre la misma serie se serializan
        // aquí: la segunda espera a que la primera haga commit o rollback.
        const string selectLineaSql = """
            SELECT "Id", "NumeroInicial", "NumeroFinal", "UltimoNumeroUsado", "Incremento", "Bloqueada"
            FROM "LineasSerie"
            WHERE "SerieId" = @SerieId AND "FechaInicial" <= @Fecha AND "Bloqueada" = false AND "IsDeleted" = false
            ORDER BY "FechaInicial" DESC
            LIMIT 1
            FOR UPDATE
            """;
        var linea = await session.Connection.QuerySingleOrDefaultAsync<LineaSerieRow?>(
            new CommandDefinition(selectLineaSql, new { SerieId = serie.Value.Id, Fecha = fecha }, session.CurrentTransaction, cancellationToken: cancellationToken));

        if (linea is null)
        {
            return Result<string>.Fallo(new Error("numeracion.sin_linea_vigente", $"La serie '{codigoSerie}' no tiene una línea vigente para la fecha {fecha}.", nameof(fecha)));
        }

        var ultimo = long.Parse(linea.UltimoNumeroUsado);
        var siguiente = ultimo + linea.Incremento;
        var maximo = long.Parse(linea.NumeroFinal);

        if (siguiente > maximo)
        {
            return Result<string>.Fallo(new Error("numeracion.serie_agotada", $"La serie '{codigoSerie}' agotó su rango numérico.", nameof(codigoSerie)));
        }

        var numeroFormateado = siguiente.ToString().PadLeft(linea.NumeroInicial.Length, '0');

        const string updateSql = """
            UPDATE "LineasSerie" SET "UltimoNumeroUsado" = @Nuevo WHERE "Id" = @Id
            """;
        await session.Connection.ExecuteAsync(
            new CommandDefinition(updateSql, new { Nuevo = siguiente.ToString(), Id = linea.Id }, session.CurrentTransaction, cancellationToken: cancellationToken));

        return Result<string>.Exito(numeroFormateado);
    }

    private sealed record LineaSerieRow(Guid Id, string NumeroInicial, string NumeroFinal, string UltimoNumeroUsado, int Incremento, bool Bloqueada);

    private sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value.ToDateTime(TimeOnly.MinValue);
        }

        public override DateOnly Parse(object value) => DateOnly.FromDateTime((DateTime)value);
    }
}
