using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OpenSource1.Infrastructure.Data;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Infrastructure;

/// <summary>
/// Migración <c>RetirarEntradasYAppSettings</c> (Task 8.2) contra Postgres real: Up() borra las tablas de los dos módulos de
/// prueba retirados; Down() las recrea vacías con exactamente el esquema (columnas, tipos, nulabilidad, defaults e índices,
/// incluido el único parcial) que dejaban las migraciones antiguas; Up() otra vez las borra. REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RetirarModulosPruebaMigrationTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    private const string MigracionAnterior = "20260926223514_AddSerieCobro";
    private static readonly string[] Tablas = ["AppSettings", "Entradas"];

    [Fact]
    public async Task Up_BorraLasTablas_YDownLasRecreaVaciasConElMismoEsquema()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.AppConnectionString).Options;
        await using var contexto = new ApplicationDbContext(options);
        var migrador = contexto.GetService<IMigrator>();
        await using var conexion = new NpgsqlConnection(fixture.AppConnectionString);

        try
        {
            // Esquema de referencia: el que construyen las migraciones antiguas.
            await migrador.MigrateAsync(MigracionAnterior);
            var columnasOriginales = await ColumnasAsync(conexion);
            var indicesOriginales = await IndicesAsync(conexion);
            Assert.Equal(2, columnasOriginales.Select(c => c.Split('|')[0]).Distinct().Count());
            Assert.Contains(indicesOriginales, i => i.Contains("UNIQUE INDEX \"IX_AppSettings_Key\"") && i.Contains("WHERE (\"IsDeleted\" = false)"));
            Assert.Equal(5, indicesOriginales.Count); // 2 PK + 2 CreatedAtUtc + Key único parcial.

            await migrador.MigrateAsync();
            Assert.Equal(0L, await ContarTablasAsync(conexion));

            await migrador.MigrateAsync(MigracionAnterior);
            Assert.Equal(columnasOriginales, await ColumnasAsync(conexion));
            Assert.Equal(indicesOriginales, await IndicesAsync(conexion));
            foreach (var tabla in Tablas)
            {
                Assert.Equal(0L, await conexion.ExecuteScalarAsync<long>($"""SELECT COUNT(*) FROM "{tabla}" """));
            }
        }
        finally
        {
            await migrador.MigrateAsync();
        }

        Assert.Equal(0L, await ContarTablasAsync(conexion));
    }

    private static Task<long> ContarTablasAsync(NpgsqlConnection conexion) =>
        conexion.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = current_schema() AND table_name = ANY(@Tablas)""",
            new { Tablas });

    private static async Task<List<string>> ColumnasAsync(NpgsqlConnection conexion) =>
        (await conexion.QueryAsync<string>(
            """
            SELECT table_name || '|' || column_name || '|' || data_type || '|' || COALESCE(character_maximum_length::text, '')
                || '|' || is_nullable || '|' || COALESCE(column_default, '')
            FROM information_schema.columns
            WHERE table_schema = current_schema() AND table_name = ANY(@Tablas)
            ORDER BY table_name, column_name
            """,
            new { Tablas })).ToList();

    private static async Task<List<string>> IndicesAsync(NpgsqlConnection conexion) =>
        (await conexion.QueryAsync<string>(
            """SELECT indexdef FROM pg_indexes WHERE schemaname = current_schema() AND tablename = ANY(@Tablas) ORDER BY indexname""",
            new { Tablas })).ToList();
}
