using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OpenSource1.Core.Common;
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

    [Fact]
    public async Task Up_SerieUsadaPorPlantillaOLoteDeDiario_QuedaActivaComoDiario()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.AppConnectionString).Options;
        await using var contexto = new ApplicationDbContext(options);
        var migrador = contexto.GetService<IMigrator>();
        await using var conexion = new NpgsqlConnection(fixture.AppConnectionString);
        var sufijo = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var dePlantilla = Guid.NewGuid();
        var deLote = Guid.NewGuid();
        var sinUso = Guid.NewGuid();
        var plantilla = Guid.NewGuid();
        var lote = Guid.NewGuid();

        try
        {
            await migrador.MigrateAsync(MigracionAnterior);
            await conexion.ExecuteAsync(
                """
                INSERT INTO "Series" ("Id", "Codigo", "Descripcion", "PermiteHuecos", "PorDefecto", "CreatedAtUtc", "CreatedBy", "IsDeleted")
                VALUES (@P, @CP, 'De plantilla', false, false, now(), 'test', false),
                       (@L, @CL, 'De lote', false, false, now(), 'test', false),
                       (@S, @CS, 'Sin uso', false, false, now(), 'test', false);
                INSERT INTO "PlantillasDiario" ("Id", "Codigo", "Nombre", "SerieId", "Tipo", "CreatedAtUtc", "CreatedBy", "IsDeleted")
                VALUES (@Pl, @CPl, 'Plantilla', @P, 1, now(), 'test', false);
                INSERT INTO "LotesDiario" ("Id", "Codigo", "Nombre", "PlantillaDiarioId", "SerieId", "Bloqueado", "CreatedAtUtc", "CreatedBy", "IsDeleted")
                VALUES (@Lo, 'LOTE1', 'Lote', @Pl, @L, false, now(), 'test', false);
                """,
                new
                {
                    P = dePlantilla, CP = $"PLA{sufijo}", L = deLote, CL = $"LOT{sufijo}", S = sinUso, CS = $"SIN{sufijo}",
                    Pl = plantilla, CPl = $"PL{sufijo}", Lo = lote,
                });

            await migrador.MigrateAsync();

            var series = (await conexion.QueryAsync<(Guid Id, short Tipo, bool Activa)>(
                """SELECT "Id", "TipoDocumento", "Activa" FROM "Series" WHERE "Id" IN (@P, @L, @S)""",
                new { P = dePlantilla, L = deLote, S = sinUso })).ToDictionary(x => x.Id, x => (x.Tipo, x.Activa));
            Assert.Equal(((short)8, true), series[dePlantilla]);
            Assert.Equal(((short)8, true), series[deLote]);
            Assert.Equal(((short)8, false), series[sinUso]);
        }
        finally
        {
            await migrador.MigrateAsync();
            await conexion.ExecuteAsync(
                """
                DELETE FROM "LotesDiario" WHERE "Id" = @Lo;
                DELETE FROM "PlantillasDiario" WHERE "Id" = @Pl;
                DELETE FROM "Series" WHERE "Id" IN (@P, @L, @S);
                """,
                new { Lo = lote, Pl = plantilla, P = dePlantilla, L = deLote, S = sinUso });
        }
    }

    [Fact]
    public async Task Up_LineasVivasDuplicadasEnLaMismaFecha_FallaConMensaje_YDeshaceLaMigracion()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.AppConnectionString).Options;
        await using var contexto = new ApplicationDbContext(options);
        var migrador = contexto.GetService<IMigrator>();
        await using var conexion = new NpgsqlConnection(fixture.AppConnectionString);
        var sufijo = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var serie = Guid.NewGuid();
        var codigo = $"DIARIO-{sufijo}";
        var primera = Guid.NewGuid();
        var duplicada = Guid.NewGuid();
        var borrada = Guid.NewGuid();

        try
        {
            await migrador.MigrateAsync(MigracionAnterior);
            await conexion.ExecuteAsync(
                """
                INSERT INTO "Series" ("Id", "Codigo", "Descripcion", "PermiteHuecos", "PorDefecto", "CreatedAtUtc", "CreatedBy", "IsDeleted")
                VALUES (@S, @C, 'Duplicada', false, false, now(), 'test', false);
                INSERT INTO "LineasSerie" ("Id", "SerieId", "NumeroInicial", "NumeroFinal", "UltimoNumeroUsado", "FechaInicial",
                                           "Incremento", "Bloqueada", "CreatedAtUtc", "CreatedBy", "IsDeleted")
                VALUES (@A, @S, '00000001', '99999999', '00000000', DATE '2021-03-04', 1, false, now(), 'test', false),
                       (@B, @S, '00000001', '99999999', '00000000', DATE '2021-03-04', 1, false, now(), 'test', false),
                       (@X, @S, '00000001', '99999999', '00000000', DATE '2021-03-04', 1, false, now(), 'test', true);
                """,
                new { S = serie, C = codigo, A = primera, B = duplicada, X = borrada });

            var error = await Assert.ThrowsAsync<PostgresException>(() => migrador.MigrateAsync());
            Assert.Equal("P0001", error.SqlState);
            Assert.Contains("SeriesNumeracionConfigurables", error.MessageText);
            Assert.Contains($"{codigo} 2021-03-04", error.MessageText);

            // Deshecha entera: sigue en la migración anterior, sin columnas nuevas y con PorDefecto.
            Assert.False(await conexion.ExecuteScalarAsync<bool>(
                """SELECT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" LIKE '%_SeriesNumeracionConfigurables')"""));
            Assert.False(await ExisteAsync(conexion, "Series", "TipoDocumento"));
            Assert.False(await ExisteAsync(conexion, "Series", "Activa"));
            Assert.False(await ExisteAsync(conexion, "ConfiguracionesNumeracion", null));
            Assert.True(await ExisteAsync(conexion, "Series", "PorDefecto"));

            // Quitada la sobrante (la borrada lógica no cuenta), la migración pasa.
            await conexion.ExecuteAsync("""DELETE FROM "LineasSerie" WHERE "Id" = @B""", new { B = duplicada });
            await migrador.MigrateAsync();
            Assert.True(await ExisteAsync(conexion, "Series", "TipoDocumento"));
        }
        finally
        {
            await conexion.ExecuteAsync(
                """
                DELETE FROM "LineasSerie" WHERE "SerieId" = @S;
                DELETE FROM "Series" WHERE "Id" = @S;
                """,
                new { S = serie });
            await migrador.MigrateAsync();
        }
    }

    [Fact]
    public async Task DownUp_ConDatos_ConservaElSiguienteNumero_YVuelveAInferirElTipo()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.AppConnectionString).Options;
        await using var contexto = new ApplicationDbContext(options);
        var migrador = contexto.GetService<IMigrator>();
        await using var conexion = new NpgsqlConnection(fixture.AppConnectionString);
        var sufijo = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var diario = Guid.NewGuid();
        var desconocida = Guid.NewGuid();
        var lineaDiario = Guid.NewGuid();
        var lineaDesconocida = Guid.NewGuid();

        try
        {
            await migrador.MigrateAsync(MigracionAnterior);
            await conexion.ExecuteAsync(
                """
                INSERT INTO "Series" ("Id", "Codigo", "Descripcion", "PermiteHuecos", "PorDefecto", "CreatedAtUtc", "CreatedBy", "IsDeleted")
                VALUES (@D, @CD, 'Diario', false, false, now(), 'test', false),
                       (@X, @CX, 'Desconocida', false, false, now(), 'test', false);
                INSERT INTO "LineasSerie" ("Id", "SerieId", "NumeroInicial", "NumeroFinal", "UltimoNumeroUsado", "FechaInicial",
                                           "Incremento", "Bloqueada", "CreatedAtUtc", "CreatedBy", "IsDeleted")
                VALUES (@LD, @D, '00000001', '99999999', '41', DATE '2020-01-01', 1, false, now(), 'test', false),
                       (@LX, @X, '000001', '999999', '000012', DATE '2020-01-01', 1, false, now(), 'test', false);
                """,
                new { D = diario, CD = $"DIARIO-{sufijo}", X = desconocida, CX = $"XYZ{sufijo}", LD = lineaDiario, LX = lineaDesconocida });

            await migrador.MigrateAsync();
            Assert.Equal("00000042", await SiguienteAsync(conexion, lineaDiario));
            Assert.Equal("000013", await SiguienteAsync(conexion, lineaDesconocida));

            // Down: el contador ya relleno lo sigue leyendo el código anterior (long.Parse) y da el mismo siguiente.
            await migrador.MigrateAsync(MigracionAnterior);
            Assert.Equal(41, long.Parse(await ContadorAsync(conexion, lineaDiario), System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(12, long.Parse(await ContadorAsync(conexion, lineaDesconocida), System.Globalization.CultureInfo.InvariantCulture));

            // Up otra vez: mismo contador, mismo siguiente número y el tipo/activa se vuelven a inferir por código.
            await migrador.MigrateAsync();
            Assert.Equal("00000041", await ContadorAsync(conexion, lineaDiario));
            Assert.Equal("00000042", await SiguienteAsync(conexion, lineaDiario));
            Assert.Equal("000013", await SiguienteAsync(conexion, lineaDesconocida));
            var series = (await conexion.QueryAsync<(Guid Id, short Tipo, bool Activa)>(
                """SELECT "Id", "TipoDocumento", "Activa" FROM "Series" WHERE "Id" IN (@D, @X)""",
                new { D = diario, X = desconocida })).ToDictionary(x => x.Id, x => (x.Tipo, x.Activa));
            Assert.Equal(((short)8, true), series[diario]);
            Assert.Equal(((short)8, false), series[desconocida]);
        }
        finally
        {
            await migrador.MigrateAsync();
            await conexion.ExecuteAsync(
                """
                DELETE FROM "LineasSerie" WHERE "Id" IN (@LD, @LX);
                DELETE FROM "Series" WHERE "Id" IN (@D, @X);
                """,
                new { LD = lineaDiario, LX = lineaDesconocida, D = diario, X = desconocida });
        }
    }

    private static Task<string> ContadorAsync(NpgsqlConnection conexion, Guid linea) =>
        conexion.ExecuteScalarAsync<string>("""SELECT "UltimoNumeroUsado" FROM "LineasSerie" WHERE "Id" = @Id""", new { Id = linea })!;

    private static async Task<string> SiguienteAsync(NpgsqlConnection conexion, Guid linea)
    {
        var l = await conexion.QuerySingleAsync<(string Inicial, string Final, string? Ultimo, int Incremento, string? Aviso)>(
            """SELECT "NumeroInicial", "NumeroFinal", "UltimoNumeroUsado", "Incremento", "NumeroAviso" FROM "LineasSerie" WHERE "Id" = @Id""",
            new { Id = linea });
        var siguiente = CalculoNumeroSerie.Siguiente("TEST", l.Inicial, l.Final, l.Ultimo, l.Incremento, l.Aviso);
        return siguiente.Valor.Numero;
    }

    private static Task<bool> ExisteAsync(NpgsqlConnection conexion, string tabla, string? columna) =>
        conexion.ExecuteScalarAsync<bool>(
            columna is null
                ? """SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = @Tabla)"""
                : """SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = @Tabla AND column_name = @Columna)""",
            new { Tabla = tabla, Columna = columna });
}
