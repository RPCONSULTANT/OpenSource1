using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OpenSource1.Infrastructure.Data;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Infrastructure;

/// <summary>
/// Verifica contra Postgres real la migración con transformación de datos <c>ExtendProducto</c> (Task 2.9): aplica las migraciones
/// hasta la anterior, siembra productos con categorías y unidades variadas (incluida una categoría de 30 caracteres con nombre de 100,
/// códigos con mayúsculas/espacios distintos, una unidad desconocida, una fila borrada lógicamente y stock distinto de cero), aplica
/// la migración y comprueba que cada producto conserva su categoría y su unidad por coincidencia, que las categorías legadas quedan
/// creadas UNA vez cada una, y que Stock y Precio no cambian; luego prueba <c>Down()</c> y <c>Up()</c> de nuevo. REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ExtendProductoMigrationTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    private const string MigracionAnterior = "20260924225654_ExtendSocioNegocio";
    private const string CategoriaGeneralSemilla = "c1000000-0000-0000-0000-000000000001";

    private static readonly string Codigo30 = new('C', 30);
    private static readonly string Nombre100 = new('N', 100);

    [Fact]
    public async Task Up_ConProductosExistentes_MapeaCategoriaYUnidadPorCodigo_ConservaStockYPrecio_YDownUpSonReversibles()
    {
        var migrador = await PrepararAsync();

        await EjecutarAsync(
            $"""
            INSERT INTO "CategoriasProducto" ("Id","Codigo","Nombre","CreatedAtUtc","CreatedBy","IsDeleted") VALUES
            ('e0000000-0000-0000-0000-000000000001','PREX','Pre existente','2026-01-01 00:00:00+00','admin',false);
            INSERT INTO "CategoriasProducto" ("Id","Codigo","Nombre","CreatedAtUtc","CreatedBy","IsDeleted","DeletedAtUtc","DeletedBy") VALUES
            ('e0000000-0000-0000-0000-000000000002','DEL','Borrada previa','2026-01-01 00:00:00+00','admin',true,'2026-02-01 00:00:00+00','admin');

            INSERT INTO "Productos" ("Id","Codigo","Nombre","Precio","Stock","CategoriaCodigo","CategoriaNombre","UnidadMedidaCodigo","UnidadMedidaNombre","CreatedAtUtc","CreatedBy","IsDeleted","DeletedAtUtc","DeletedBy") VALUES
            ('11111111-1111-1111-1111-111111111111','P1','Uno',10.55,7,'ELEC','Electrónica','KG','Kilogramo','2026-01-01 10:00:00+00','admin',false,NULL,NULL),
            ('22222222-2222-2222-2222-222222222222','P2','Dos',20.00,3,'ELEC','Electronica otro nombre','UND','Unidad','2026-01-02 10:00:00+00','admin',false,NULL,NULL),
            ('33333333-3333-3333-3333-333333333333','P3','Tres',0.05,0,'GENERAL','General','LT','Litro','2026-01-03 10:00:00+00','admin',false,NULL,NULL),
            ('44444444-4444-4444-4444-444444444444','P4','Cuatro',1.00,11,' alim ','Alimentos','PAQ','Paquete','2026-01-04 10:00:00+00','admin',false,NULL,NULL),
            ('55555555-5555-5555-5555-555555555555','P5','Cinco',2.50,12,'ALIM','Alimentos','xx','Desconocida','2026-01-05 10:00:00+00','admin',false,NULL,NULL),
            ('66666666-6666-6666-6666-666666666666','P6','Borrado',5.00,5,'BORR','Categoria de un borrado','CJA','Caja','2026-01-06 10:00:00+00','admin',true,'2026-02-01 10:00:00+00','admin'),
            ('77777777-7777-7777-7777-777777777777','P7','Largo',1234.50,9,'{Codigo30}','{Nombre100}','MT','Metro','2026-01-07 10:00:00+00','admin',false,NULL,NULL),
            ('88888888-8888-8888-8888-888888888888','P8','Sin categoria',3.00,4,'','','ml','Mililitro','2026-01-08 10:00:00+00','admin',false,NULL,NULL),
            ('99999999-9999-9999-9999-999999999999','P9','Preexistente',4.00,2,'prex','Otro nombre','GR','Gramo','2026-01-09 10:00:00+00','admin',false,NULL,NULL),
            ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','P10','Categoria borrada',6.00,1,'del','Del','DOC','Docena','2026-01-10 10:00:00+00','admin',false,NULL,NULL);
            """);

        await migrador.MigrateAsync();

        // 1. Cada producto conserva su categoría y su unidad por coincidencia de código (o el destino por defecto).
        var filas = await LeerAsync(
            """
            SELECT p."Codigo", c."Codigo", c."Nombre", u."Codigo", p."Stock", p."PrecioVenta", p."IsDeleted"
            FROM "Productos" p
            JOIN "CategoriasProducto" c ON c."Id" = p."CategoriaId"
            JOIN "UnidadesMedida" u ON u."Id" = p."UnidadMedidaBaseId"
            ORDER BY p."CreatedAtUtc"
            """);

        Assert.Equal(10, filas.Count);
        Assert.Equal(["P1", "P2", "P3", "P4", "P5", "P6", "P7", "P8", "P9", "P10"], filas.Select(f => (string)f[0]!));
        Assert.Equal(["ELEC", "ELEC", "GENERAL", "ALIM", "ALIM", "BORR", Codigo30, "GENERAL", "PREX", "DEL"], filas.Select(f => (string)f[1]!));
        Assert.Equal(["KG", "UND", "LT", "PAQ", "UND", "CJA", "MT", "ML", "GR", "DOC"], filas.Select(f => (string)f[3]!));

        // Si un mismo código aparece con nombres distintos gana el del producto más antiguo.
        Assert.Equal("Electrónica", filas[0][2]);
        Assert.Equal("Electrónica", filas[1][2]);
        Assert.Equal("Alimentos", filas[3][2]);
        // El de 30 caracteres con nombre de 100 cabe entero.
        Assert.Equal(Nombre100, filas[6][2]);
        // La categoría preexistente NO se duplica ni cambia de nombre; la borrada previa no cuenta como coincidencia.
        Assert.Equal("Pre existente", filas[8][2]);
        Assert.Equal("Del", filas[9][2]);

        // 2. Stock y Precio no cambian (Precio -> PrecioVenta sin perder decimales).
        Assert.Equal([7, 3, 0, 11, 12, 5, 9, 4, 2, 1], filas.Select(f => (int)f[4]!));
        Assert.Equal([10.55m, 20.00m, 0.05m, 1.00m, 2.50m, 5.00m, 1234.50m, 3.00m, 4.00m, 6.00m], filas.Select(f => (decimal)f[5]!));
        Assert.True((bool)filas[5][6]!);

        // 3. Las categorías legadas quedaron creadas UNA vez cada una (activas), sin duplicados por mayúsculas/espacios.
        var conteos = await LeerAsync(
            """
            SELECT "Codigo", COUNT(*) FROM "CategoriasProducto" WHERE "IsDeleted" = false GROUP BY "Codigo" ORDER BY "Codigo"
            """);
        Assert.All(conteos, c => Assert.Equal(1L, c[1]));
        Assert.Equal(["ALIM", "BORR", Codigo30, "DEL", "ELEC", "GENERAL", "PREX"], conteos.Select(c => (string)c[0]!).Order(StringComparer.Ordinal));

        var creadas = await LeerAsync("""SELECT "CreatedBy", "IsDeleted" FROM "CategoriasProducto" WHERE "Codigo" IN ('ELEC','ALIM','BORR') """);
        Assert.Equal(3, creadas.Count);
        Assert.All(creadas, c =>
        {
            Assert.Equal("system", c[0]);
            Assert.False((bool)c[1]!);
        });

        // 4. Columnas nuevas con el valor de negocio y SIN default de BD; las legadas ya no existen.
        var nuevas = await LeerAsync("""SELECT "MetodoCosteo", "CostoUnitario", "CostoEstandar", "CostoAjustado", "Bloqueado" FROM "Productos" """);
        Assert.All(nuevas, f =>
        {
            Assert.Equal((short)1, f[0]);
            Assert.Equal(0m, f[1]);
            Assert.Equal(0m, f[2]);
            Assert.True((bool)f[3]!);
            Assert.Equal((short)0, f[4]);
        });
        var columnas = await LeerAsync(
            """
            SELECT column_name, column_default, data_type, numeric_scale FROM information_schema.columns
            WHERE table_name = 'Productos' AND table_schema = current_schema()
            """);
        var nombres = columnas.Select(c => (string)c[0]!).ToList();
        Assert.All(new[] { "CategoriaCodigo", "CategoriaNombre", "UnidadMedidaCodigo", "UnidadMedidaNombre", "Precio" }, legada => Assert.DoesNotContain(legada, nombres));
        Assert.All(new[] { "MetodoCosteo", "CostoUnitario", "CostoEstandar", "CostoAjustado", "Bloqueado", "CategoriaId", "UnidadMedidaBaseId" },
            nueva => Assert.Null(columnas.Single(c => (string)c[0]! == nueva)[1]));
        Assert.Equal((int)4, columnas.Single(c => (string)c[0]! == "PrecioVenta")[3]);
        Assert.Contains("Stock", nombres);

        // 5. Las FK existen y están endurecidas.
        var fks = (await LeerAsync("""SELECT conname FROM pg_constraint WHERE conrelid = '"Productos"'::regclass AND contype = 'f'""")).Select(f => (string)f[0]!).ToList();
        Assert.Contains("FK_Productos_CategoriasProducto_CategoriaId", fks);
        Assert.Contains("FK_Productos_UnidadesMedida_UnidadMedidaBaseId", fks);

        // 6. Down(): recompone categoría y unidad legadas por Id, Precio con escala 2, Stock intacto.
        await migrador.MigrateAsync(MigracionAnterior);
        var abajo = await LeerAsync(
            """SELECT "Codigo", "CategoriaCodigo", "CategoriaNombre", "UnidadMedidaCodigo", "UnidadMedidaNombre", "Precio", "Stock" FROM "Productos" ORDER BY "CreatedAtUtc" """);
        Assert.Equal(10, abajo.Count);
        Assert.Equal("ELEC", abajo[0][1]);
        Assert.Equal("Electrónica", abajo[0][2]);
        Assert.Equal("KG", abajo[0][3]);
        Assert.Equal("Kilogramo", abajo[0][4]);
        Assert.Equal(10.55m, abajo[0][5]);
        Assert.Equal(7, abajo[0][6]);
        Assert.Equal(Codigo30, abajo[6][1]);
        Assert.Equal(Nombre100, abajo[6][2]);
        Assert.Equal("UND", abajo[4][3]);
        Assert.Equal([7, 3, 0, 11, 12, 5, 9, 4, 2, 1], abajo.Select(f => (int)f[6]!));

        // 7. Up() de nuevo: mismo resultado, sin categorías duplicadas.
        await migrador.MigrateAsync();
        var otraVez = await LeerAsync(
            """
            SELECT p."Codigo", c."Codigo", u."Codigo" FROM "Productos" p
            JOIN "CategoriasProducto" c ON c."Id" = p."CategoriaId" JOIN "UnidadesMedida" u ON u."Id" = p."UnidadMedidaBaseId"
            ORDER BY p."CreatedAtUtc"
            """);
        Assert.Equal(["ELEC", "ELEC", "GENERAL", "ALIM", "ALIM", "BORR", Codigo30, "GENERAL", "PREX", "DEL"], otraVez.Select(f => (string)f[1]!));
        Assert.Equal(["KG", "UND", "LT", "PAQ", "UND", "CJA", "MT", "ML", "GR", "DOC"], otraVez.Select(f => (string)f[2]!));
        var conteo2 = await LeerAsync("""SELECT "Codigo", COUNT(*) FROM "CategoriasProducto" WHERE "IsDeleted" = false GROUP BY "Codigo" HAVING COUNT(*) > 1""");
        Assert.Empty(conteo2);
    }

    [Fact]
    public async Task Up_ConGeneralYUndBorradosLogicamente_LosRecreaYLosUsaComoDestinoPorDefecto()
    {
        var migrador = await PrepararAsync();

        await EjecutarAsync(
            $"""
            UPDATE "CategoriasProducto" SET "IsDeleted" = true, "DeletedAtUtc" = now(), "DeletedBy" = 'admin' WHERE "Id" = '{CategoriaGeneralSemilla}';
            UPDATE "UnidadesMedida" SET "IsDeleted" = true, "DeletedAtUtc" = now(), "DeletedBy" = 'admin' WHERE "Codigo" = 'UND';
            INSERT INTO "Productos" ("Id","Codigo","Nombre","Precio","Stock","CategoriaCodigo","CategoriaNombre","UnidadMedidaCodigo","UnidadMedidaNombre","CreatedAtUtc","CreatedBy","IsDeleted") VALUES
            ('11111111-1111-1111-1111-111111111111','Q1','Sin coincidencia',1.00,1,'','','ZZZ','Rara','2026-01-01 10:00:00+00','admin',false);
            """);

        await migrador.MigrateAsync();

        var filas = await LeerAsync(
            """
            SELECT c."Codigo", c."IsDeleted", u."Codigo", u."IsDeleted" FROM "Productos" p
            JOIN "CategoriasProducto" c ON c."Id" = p."CategoriaId" JOIN "UnidadesMedida" u ON u."Id" = p."UnidadMedidaBaseId"
            """);
        var fila = Assert.Single(filas);
        Assert.Equal("GENERAL", fila[0]);
        Assert.False((bool)fila[1]!);
        Assert.Equal("UND", fila[2]);
        Assert.False((bool)fila[3]!);
    }

    private async Task<IMigrator> PrepararAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.AppConnectionString).Options;
        var contexto = new ApplicationDbContext(options);
        var migrador = contexto.GetService<IMigrator>();

        // Estado de partida idéntico en cada prueba: esquema anterior a ExtendProducto, sin productos, y solo el catálogo sembrado.
        await migrador.MigrateAsync(MigracionAnterior);
        await EjecutarAsync(
            $"""
            DELETE FROM "UnidadesMedidaProducto";
            DELETE FROM "Productos";
            DELETE FROM "CategoriasProducto" WHERE "Id" <> '{CategoriaGeneralSemilla}';
            UPDATE "CategoriasProducto" SET "IsDeleted" = false, "DeletedAtUtc" = NULL, "DeletedBy" = NULL WHERE "Id" = '{CategoriaGeneralSemilla}';
            DELETE FROM "UnidadesMedida" WHERE "Id"::text NOT LIKE 'a1000000-0000-0000-0000-0000000000%';
            UPDATE "UnidadesMedida" SET "IsDeleted" = false, "DeletedAtUtc" = NULL, "DeletedBy" = NULL;
            """);
        return migrador;
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
