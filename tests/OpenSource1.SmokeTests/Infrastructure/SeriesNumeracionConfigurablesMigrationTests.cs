using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Clientes;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Entities.Inventario;
using OpenSource1.Core.Entities.Ventas;
using OpenSource1.Infrastructure.Data;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Infrastructure;

/// <summary>
/// Migración <c>SeriesNumeracionConfigurables</c> (spec no-series, Parte 1) contra Postgres real: infiere el tipo de cada serie por
/// su código (las desconocidas quedan como diario de inventario INACTIVO), siembra la configuración por tipo, normaliza los
/// contadores sin relleno y Down()/Up() es reversible. REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SeriesNumeracionConfigurablesMigrationTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    private const string MigracionAnterior = "20260927155353_AddNotasCreditoVenta";

    [Fact]
    public async Task Up_InfiereElTipo_SiembraLaConfiguracion_NormalizaContadores_YDownUpEsReversible()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.AppConnectionString).Options;
        await using var contexto = new ApplicationDbContext(options);
        var migrador = contexto.GetService<IMigrator>();
        await using var conexion = new NpgsqlConnection(fixture.AppConnectionString);
        var sufijo = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var desconocida = Guid.NewGuid();
        var diarioPropio = Guid.NewGuid();
        var lineaSinRelleno = Guid.NewGuid();

        try
        {
            await migrador.MigrateAsync(MigracionAnterior);
            await conexion.ExecuteAsync(
                """
                INSERT INTO "Series" ("Id", "Codigo", "Descripcion", "PermiteHuecos", "PorDefecto", "CreatedAtUtc", "CreatedBy", "IsDeleted")
                VALUES (@D, @CD, 'Desconocida', false, false, now(), 'test', false),
                       (@G, @CG, 'Diario propio', false, false, now(), 'test', false);
                INSERT INTO "LineasSerie" ("Id", "SerieId", "NumeroInicial", "NumeroFinal", "UltimoNumeroUsado", "FechaInicial",
                                           "Incremento", "Bloqueada", "CreatedAtUtc", "CreatedBy", "IsDeleted")
                VALUES (@L, @G, '00000001', '99999999', '7', DATE '2020-01-01', 1, false, now(), 'test', false);
                """,
                new { D = desconocida, CD = $"XYZ{sufijo}", G = diarioPropio, CG = $"DIARIO-{sufijo}", L = lineaSinRelleno });

            await migrador.MigrateAsync();

            var tipos = (await conexion.QueryAsync<(string Codigo, short Tipo, bool Activa)>(
                """
                SELECT "Codigo", "TipoDocumento", "Activa" FROM "Series"
                WHERE "Codigo" IN ('FV-BORR', 'FV', 'NC-BORR', 'NC', 'COBRO', 'CONTAB', 'SOCIOS', 'DIARIO-INV')
                """)).ToDictionary(x => x.Codigo, x => (x.Tipo, x.Activa));
            Assert.Equal((short)1, tipos["FV-BORR"].Tipo);
            Assert.Equal((short)2, tipos["FV"].Tipo);
            Assert.Equal((short)3, tipos["NC-BORR"].Tipo);
            Assert.Equal((short)4, tipos["NC"].Tipo);
            Assert.Equal((short)5, tipos["COBRO"].Tipo);
            Assert.Equal((short)6, tipos["CONTAB"].Tipo);
            Assert.Equal((short)7, tipos["SOCIOS"].Tipo);
            Assert.Equal((short)8, tipos["DIARIO-INV"].Tipo);
            Assert.All(tipos.Values, t => Assert.True(t.Activa));

            Assert.Equal(((short)8, false), await conexion.QuerySingleAsync<(short, bool)>(
                """SELECT "TipoDocumento", "Activa" FROM "Series" WHERE "Id" = @Id""", new { Id = desconocida }));
            Assert.Equal(((short)8, true), await conexion.QuerySingleAsync<(short, bool)>(
                """SELECT "TipoDocumento", "Activa" FROM "Series" WHERE "Id" = @Id""", new { Id = diarioPropio }));
            Assert.Equal("00000007", await conexion.ExecuteScalarAsync<string>(
                """SELECT "UltimoNumeroUsado" FROM "LineasSerie" WHERE "Id" = @Id""", new { Id = lineaSinRelleno }));

            var configuracion = (await conexion.QueryAsync<(short Tipo, Guid SerieId)>(
                """SELECT "TipoDocumento", "SerieId" FROM "ConfiguracionesNumeracion" WHERE NOT "IsDeleted" ORDER BY "TipoDocumento" """)).ToList();
            Assert.Equal(
            [
                ((short)1, SerieFacturaVentaIds.SerieBorradorId), ((short)2, SerieFacturaVentaIds.SeriePosteadaId),
                ((short)3, SerieNotaCreditoVentaIds.SerieBorradorId), ((short)4, SerieNotaCreditoVentaIds.SeriePosteadaId),
                ((short)5, SerieCobroIds.SerieId), ((short)6, SerieContabilidadIds.SerieId),
                ((short)7, SerieClienteIds.SerieId), ((short)8, SerieDiarioInventarioIds.SerieId),
            ], configuracion);

            // El CK impide una serie sin tipo.
            var sinTipo = await Assert.ThrowsAsync<PostgresException>(() => conexion.ExecuteAsync(
                """UPDATE "Series" SET "TipoDocumento" = 0 WHERE "Id" = @Id""", new { Id = desconocida }));
            Assert.Equal("23514", sinTipo.SqlState);

            // Down: sin tabla ni columnas nuevas y con PorDefecto de vuelta.
            await migrador.MigrateAsync(MigracionAnterior);
            Assert.False(await ExisteAsync(conexion, "ConfiguracionesNumeracion", null));
            Assert.False(await ExisteAsync(conexion, "Series", "TipoDocumento"));
            Assert.False(await ExisteAsync(conexion, "Series", "Activa"));
            Assert.False(await ExisteAsync(conexion, "LineasSerie", "NumeroAviso"));
            Assert.True(await ExisteAsync(conexion, "Series", "PorDefecto"));
        }
        finally
        {
            await migrador.MigrateAsync();
            await conexion.ExecuteAsync(
                """
                DELETE FROM "LineasSerie" WHERE "Id" = @L;
                DELETE FROM "Series" WHERE "Id" IN (@D, @G);
                """,
                new { L = lineaSinRelleno, D = desconocida, G = diarioPropio });
        }
    }

    private static Task<bool> ExisteAsync(NpgsqlConnection conexion, string tabla, string? columna) =>
        conexion.ExecuteScalarAsync<bool>(
            columna is null
                ? """SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = @Tabla)"""
                : """SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = @Tabla AND column_name = @Columna)""",
            new { Tabla = tabla, Columna = columna });
}
