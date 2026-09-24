using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OpenSource1.Infrastructure.Data;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Infrastructure;

/// <summary>
/// Verifica contra Postgres real la migración con transformación de datos
/// <c>ExtendSocioNegocio</c> (Task 2.6): aplica las migraciones hasta la anterior, siembra filas
/// (completa, con nulos, borrada lógicamente y con Nombre+Apellido de 100 caracteres cada uno),
/// aplica la migración y comprueba los datos; luego prueba <c>Down()</c> y <c>Up()</c> de nuevo.
/// REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ExtendSocioNegocioMigrationTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    private const string MigracionAnterior = "20260924220649_RenameClienteToSocioNegocio";

    [Fact]
    public async Task Up_ConFilasExistentes_MigraLosDatosSiembraLaSerieYDownUpSonReversibles()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.AppConnectionString).Options;
        await using var contexto = new ApplicationDbContext(options);
        var migrador = contexto.GetService<IMigrator>();

        await migrador.MigrateAsync(MigracionAnterior);
        await EjecutarAsync(
            """
            INSERT INTO "SociosNegocio" ("Id","Nombre","Apellido","Email","Telefono","DireccionLinea1","PaisCodigo","PaisNombre","Sector","CreatedAtUtc","CreatedBy","IsDeleted") VALUES
            ('11111111-1111-1111-1111-111111111111','Ana','Pérez','ana@test.local','809-555-0001','Calle 1','DO','República Dominicana','Retail','2026-01-01 10:00:00+00','admin',false),
            ('22222222-2222-2222-2222-222222222222','Juan','López','juan@test.local',NULL,NULL,NULL,NULL,NULL,'2026-01-02 10:00:00+00','admin',false);
            INSERT INTO "SociosNegocio" ("Id","Nombre","Apellido","Email","CreatedAtUtc","CreatedBy","IsDeleted","DeletedAtUtc","DeletedBy") VALUES
            ('33333333-3333-3333-3333-333333333333','Borrado','Cliente','borrado@test.local','2026-01-03 10:00:00+00','admin',true,'2026-02-01 10:00:00+00','admin');
            INSERT INTO "SociosNegocio" ("Id","Nombre","Apellido","Email","CreatedAtUtc","CreatedBy","IsDeleted") VALUES
            ('44444444-4444-4444-4444-444444444444',repeat('N',100),repeat('A',100),'largo@test.local','2026-01-04 10:00:00+00','admin',false);
            """);

        await migrador.MigrateAsync();

        var filas = await LeerAsync(
            """
            SELECT "Codigo", "NombreComercial", "Tipo", "TipoDocumentoFiscal", "IsDeleted", "Email", "Telefono", "LimiteCredito", "Bloqueado"
            FROM "SociosNegocio" ORDER BY "Codigo"
            """);

        Assert.Equal(4, filas.Count);
        // Códigos de 6 dígitos, sin duplicados, en orden de CreatedAtUtc e incluyendo la fila borrada.
        Assert.Equal(["000001", "000002", "000003", "000004"], filas.Select(f => (string)f[0]!));
        Assert.Equal("Ana Pérez", filas[0][1]);
        Assert.Equal("Juan López", filas[1][1]);
        Assert.Equal("Borrado Cliente", filas[2][1]);
        Assert.True((bool)filas[2][4]!);
        // Fila límite: 100 + espacio + 100 = 201 caracteres, truncada a 200 en vez de fallar.
        Assert.Equal(200, ((string)filas[3][1]!).Length);
        Assert.All(filas, f =>
        {
            Assert.Equal((short)1, f[2]);
            Assert.Equal((short)9, f[3]);
            Assert.Equal(0m, f[7]);
            Assert.Equal((short)0, f[8]);
        });
        // Los datos existentes sobreviven.
        Assert.Equal("ana@test.local", filas[0][5]);
        Assert.Equal("809-555-0001", filas[0][6]);
        Assert.Null(filas[1][6]);

        // La serie SOCIOS queda con el contador en el total de filas migradas (incluida la borrada).
        var serie = await LeerAsync(
            """
            SELECT l."UltimoNumeroUsado", l."NumeroInicial", l."NumeroFinal", s."PermiteHuecos"
            FROM "LineasSerie" l JOIN "Series" s ON s."Id" = l."SerieId" WHERE s."Codigo" = 'SOCIOS'
            """);
        Assert.Single(serie);
        Assert.Equal("000004", serie[0][0]);
        Assert.Equal("000001", serie[0][1]);
        Assert.Equal("999999", serie[0][2]);
        Assert.False((bool)serie[0][3]!);

        // Los índices trigram: el de NombreComercial existe, los de Nombre/Apellido ya no.
        var indices = (await LeerAsync("""SELECT indexname FROM pg_indexes WHERE tablename = 'SociosNegocio'"""))
            .Select(f => (string)f[0]!).ToList();
        Assert.Contains("IX_SociosNegocio_NombreComercial_trgm", indices);
        Assert.Contains("IX_SociosNegocio_Codigo", indices);
        Assert.Contains("IX_SociosNegocio_NumeroDocumentoFiscal", indices);
        Assert.DoesNotContain("IX_SociosNegocio_Nombre_trgm", indices);
        Assert.DoesNotContain("IX_SociosNegocio_Apellido_trgm", indices);

        // Down(): recompone Nombre/Apellido, restaura los trigram y quita la serie.
        await migrador.MigrateAsync(MigracionAnterior);
        var abajo = await LeerAsync("""SELECT "Nombre", "Apellido" FROM "SociosNegocio" WHERE "Id" = '11111111-1111-1111-1111-111111111111'""");
        Assert.Equal("Ana", abajo[0][0]);
        Assert.Equal("Pérez", abajo[0][1]);
        var indicesAbajo = (await LeerAsync("""SELECT indexname FROM pg_indexes WHERE tablename = 'SociosNegocio'"""))
            .Select(f => (string)f[0]!).ToList();
        Assert.Contains("IX_SociosNegocio_Nombre_trgm", indicesAbajo);
        Assert.Contains("IX_SociosNegocio_Apellido_trgm", indicesAbajo);
        Assert.DoesNotContain("IX_SociosNegocio_NombreComercial_trgm", indicesAbajo);
        Assert.Empty(await LeerAsync("""SELECT 1 FROM "Series" WHERE "Codigo" = 'SOCIOS'"""));

        // Up() de nuevo: mismo resultado.
        await migrador.MigrateAsync();
        var otraVez = await LeerAsync("""SELECT "Codigo", "NombreComercial" FROM "SociosNegocio" ORDER BY "Codigo" """);
        Assert.Equal(["000001", "000002", "000003", "000004"], otraVez.Select(f => (string)f[0]!));
        Assert.Equal("Ana Pérez", otraVez[0][1]);
        var serie2 = await LeerAsync("""SELECT l."UltimoNumeroUsado" FROM "LineasSerie" l JOIN "Series" s ON s."Id" = l."SerieId" WHERE s."Codigo" = 'SOCIOS'""");
        Assert.Equal("000004", serie2[0][0]);
    }

    private async Task EjecutarAsync(string sql)
    {
        await using var conexion = new NpgsqlConnection(fixture.AppConnectionString);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        await comando.ExecuteNonQueryAsync();
    }

    private async Task<List<object?[]>> LeerAsync(string sql)
    {
        await using var conexion = new NpgsqlConnection(fixture.AppConnectionString);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        await using var lector = await comando.ExecuteReaderAsync();

        var filas = new List<object?[]>();
        while (await lector.ReadAsync())
        {
            var fila = new object?[lector.FieldCount];
            for (var i = 0; i < fila.Length; i++)
            {
                fila[i] = await lector.IsDBNullAsync(i) ? null : lector.GetValue(i);
            }

            filas.Add(fila);
        }

        return filas;
    }
}
