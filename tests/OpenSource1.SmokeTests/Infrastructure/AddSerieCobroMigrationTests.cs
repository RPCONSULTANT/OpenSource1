using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OpenSource1.Core.Entities.Clientes;
using OpenSource1.Infrastructure.Data;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Infrastructure;

/// <summary>
/// Migración <c>AddSerieCobro</c> (Task 6.5) contra Postgres real: siembra la serie <c>COBRO</c> (sin huecos) con su línea vigente
/// de 8 dígitos; Down() las quita y Up() las repone. Como la base de datos se comparte con los tests de cobros, el contador de la
/// línea se restaura al valor que tenía (si no, los números COBRO ya usados se repetirían). REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AddSerieCobroMigrationTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    private const string MigracionAnterior = "20260926214836_RedesPosteoFacturasVenta";

    [Fact]
    public async Task Up_SiembraLaSerieCobroSinHuecos_YDownUpEsReversible()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.AppConnectionString).Options;
        await using var contexto = new ApplicationDbContext(options);
        var migrador = contexto.GetService<IMigrator>();
        await migrador.MigrateAsync();
        await using var conexion = new NpgsqlConnection(fixture.AppConnectionString);

        await AssertSembradaAsync(conexion);
        var ultimo = await conexion.ExecuteScalarAsync<string>(
            """SELECT "UltimoNumeroUsado" FROM "LineasSerie" WHERE "Id" = @Id""", new { Id = SerieCobroIds.LineaSerieId });

        try
        {
            await migrador.MigrateAsync(MigracionAnterior);
            Assert.Equal(0L, await conexion.ExecuteScalarAsync<long>(
                """SELECT COUNT(*) FROM "Series" WHERE "Codigo" = 'COBRO' OR "Id" = @Id""", new { Id = SerieCobroIds.SerieId }));
            Assert.Equal(0L, await conexion.ExecuteScalarAsync<long>(
                """SELECT COUNT(*) FROM "LineasSerie" WHERE "Id" = @Id""", new { Id = SerieCobroIds.LineaSerieId }));
        }
        finally
        {
            await migrador.MigrateAsync();
            await conexion.ExecuteAsync(
                """UPDATE "LineasSerie" SET "UltimoNumeroUsado" = @U WHERE "Id" = @Id""", new { U = ultimo, Id = SerieCobroIds.LineaSerieId });
        }

        await AssertSembradaAsync(conexion);
    }

    private static async Task AssertSembradaAsync(NpgsqlConnection conexion)
    {
        var serie = await conexion.QuerySingleAsync<(string Codigo, bool PermiteHuecos, bool IsDeleted)>(
            """SELECT "Codigo", "PermiteHuecos", "IsDeleted" FROM "Series" WHERE "Id" = @Id""", new { Id = SerieCobroIds.SerieId });
        Assert.Equal((SerieCobroIds.Codigo, false, false), serie);
        var linea = await conexion.QuerySingleAsync<(Guid SerieId, string NumeroInicial, string NumeroFinal, bool Bloqueada)>(
            """SELECT "SerieId", "NumeroInicial", "NumeroFinal", "Bloqueada" FROM "LineasSerie" WHERE "Id" = @Id""",
            new { Id = SerieCobroIds.LineaSerieId });
        Assert.Equal((SerieCobroIds.SerieId, "00000001", "99999999", false), linea);
    }
}
