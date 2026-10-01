using Dapper;
using Npgsql;
using OpenSource1.Core.Enums;

namespace OpenSource1.SmokeTests.TestInfrastructure;

/// <summary>
/// Series propias de una prueba (spec no-series): prefijo único <c>T…-</c>, 6 dígitos, línea vigente desde 2020 sin usar. Un prefijo
/// propio nunca se solapa con las series sembradas ni con las de otras pruebas, así que no toca la numeración compartida.
/// </summary>
public static class SeriesPrueba
{
    public static async Task<(Guid SerieId, Guid LineaId, string Prefijo)> CrearAsync(
        string connectionString, TipoDocumentoSerie tipo, bool activa = true, long? aviso = null)
    {
        var serieId = Guid.NewGuid();
        var lineaId = Guid.NewGuid();
        var prefijo = $"T{Guid.NewGuid():N}"[..6].ToUpperInvariant() + "-";
        await EjecutarAsync(connectionString,
            """
            INSERT INTO "Series" ("Id", "Codigo", "Descripcion", "TipoDocumento", "PermiteHuecos", "Activa", "CreatedAtUtc", "CreatedBy", "IsDeleted")
            VALUES (@SerieId, @Codigo, 'Serie de prueba', @Tipo, false, @Activa, now(), 'test', false);
            INSERT INTO "LineasSerie" ("Id", "SerieId", "NumeroInicial", "NumeroFinal", "NumeroAviso", "UltimoNumeroUsado", "FechaInicial",
                                       "Incremento", "Bloqueada", "CreatedAtUtc", "CreatedBy", "IsDeleted")
            VALUES (@LineaId, @SerieId, @Inicial, @Final, @Aviso, '', DATE '2020-01-01', 1, false, now(), 'test', false);
            """,
            new
            {
                SerieId = serieId, LineaId = lineaId, Codigo = $"S{Guid.NewGuid():N}"[..12].ToUpperInvariant(), Tipo = (short)tipo,
                Activa = activa, Inicial = $"{prefijo}000001", Final = $"{prefijo}999999",
                Aviso = aviso is { } a ? $"{prefijo}{a:D6}" : null,
            });
        return (serieId, lineaId, prefijo);
    }

    public static async Task<string> UltimoAsync(string connectionString, Guid lineaId)
    {
        await using var conexion = new NpgsqlConnection(connectionString);
        return (await conexion.ExecuteScalarAsync<string>(
            """SELECT "UltimoNumeroUsado" FROM "LineasSerie" WHERE "Id" = @Id""", new { Id = lineaId }))!;
    }

    public static async Task EjecutarAsync(string connectionString, string sql, object? parametros = null)
    {
        await using var conexion = new NpgsqlConnection(connectionString);
        await conexion.ExecuteAsync(sql, parametros);
    }
}
