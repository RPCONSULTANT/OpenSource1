using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OpenSource1.Infrastructure.Data;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Infrastructure;

/// <summary>
/// Verifica contra Postgres real la migración con transformación de datos <c>ReemplazarStockPorLibro</c> (Task 3.6): aplica
/// las migraciones hasta la anterior, siembra productos con Stock legado positivo, cero, negativo, un borrado lógicamente y
/// uno con unidad base KG, aplica la migración y comprueba que la existencia derivada del libro coincide con el Stock
/// legado, que el valor migrado es Stock x CostoUnitario, que solo la entrada (Stock &gt; 0) nace con CantidadRestante, que
/// Stock = 0 no genera movimiento, que la columna "Stock" ya no existe, que el Ruling AR deja CostoAjustado = false SOLO en
/// los de Stock negativo, y que Down()/Up() son reversibles (Up() no duplica movimientos al reejecutarse). REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ReemplazarStockPorLibroMigrationTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    private const string MigracionAnterior = "20260925144612_AddLibroInventario";
    private const string CategoriaGeneralSemilla = "c1000000-0000-0000-0000-000000000001";
    private const string UnidadUndSemilla = "a1000000-0000-0000-0000-000000000001";
    private const string UnidadKgSemilla = "a1000000-0000-0000-0000-000000000002";
    private const string AlmacenPrincipalSemilla = "b1000000-0000-0000-0000-000000000001";

    private static readonly Guid ProductoPositivo = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProductoCero = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ProductoNegativo = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ProductoBorrado = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ProductoKg = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public async Task Up_ConStockPositivoCeroNegativoYBorrado_MigraLaApertura_AjustaCostoAjustadoDeLosNegativos_YDownUpSonReversibles()
    {
        var migrador = await PrepararAsync();

        await EjecutarAsync($"""
            INSERT INTO "Productos" ("Id","Codigo","Nombre","PrecioVenta","Stock","CategoriaId","UnidadMedidaBaseId",
                "MetodoCosteo","CostoUnitario","CostoEstandar","CostoAjustado","Bloqueado","CreatedAtUtc","CreatedBy","IsDeleted") VALUES
            ('{ProductoPositivo}','POS','Positivo',10.00,120,'{CategoriaGeneralSemilla}','{UnidadUndSemilla}',1,2.5000,0,true,0,'2026-01-01 00:00:00+00','test',false),
            ('{ProductoCero}','CERO','Cero',10.00,0,'{CategoriaGeneralSemilla}','{UnidadUndSemilla}',1,0,0,true,0,'2026-01-01 00:00:00+00','test',false),
            ('{ProductoNegativo}','NEG','Negativo',10.00,-5,'{CategoriaGeneralSemilla}','{UnidadUndSemilla}',1,4.0000,0,true,0,'2026-01-01 00:00:00+00','test',false),
            ('{ProductoBorrado}','DEL','Borrado',10.00,7,'{CategoriaGeneralSemilla}','{UnidadUndSemilla}',1,6.0000,0,true,0,'2026-01-01 00:00:00+00','test',true),
            ('{ProductoKg}','KG1','Con unidad KG',10.00,15,'{CategoriaGeneralSemilla}','{UnidadKgSemilla}',1,1.2500,0,true,0,'2026-01-01 00:00:00+00','test',false);
            UPDATE "Productos" SET "DeletedAtUtc" = '2026-02-01 00:00:00+00', "DeletedBy" = 'test' WHERE "Id" = '{ProductoBorrado}';
            """);

        await migrador.MigrateAsync();

        // 1. Existencia derivada (SUM(Cantidad) del libro) = Stock legado; sin fila para el de Stock 0.
        var existencias = await LeerExistenciasAsync();
        Assert.Equal(120m, existencias.GetValueOrDefault(ProductoPositivo));
        Assert.False(existencias.ContainsKey(ProductoCero));
        Assert.Equal(-5m, existencias.GetValueOrDefault(ProductoNegativo));
        Assert.Equal(7m, existencias.GetValueOrDefault(ProductoBorrado));
        Assert.Equal(15m, existencias.GetValueOrDefault(ProductoKg));

        // 2. Valor migrado = Stock (con signo) x CostoUnitario vigente.
        var valores = await LeerAsync(
            """SELECT "ProductoId", SUM("ImporteCosto") FROM "MovimientosValor" WHERE "ClaveOrigen" = 'MIGRACION-STOCK' GROUP BY "ProductoId" """);
        var valorPorProducto = valores.ToDictionary(f => (Guid)f[0]!, f => (decimal)f[1]!);
        Assert.Equal(300.0000m, valorPorProducto[ProductoPositivo]);
        Assert.Equal(-20.0000m, valorPorProducto[ProductoNegativo]);
        Assert.Equal(42.0000m, valorPorProducto[ProductoBorrado]);
        Assert.Equal(18.7500m, valorPorProducto[ProductoKg]);
        Assert.False(valorPorProducto.ContainsKey(ProductoCero));

        // 3. Solo la entrada (Stock > 0) nace con CantidadRestante = Cantidad completa; la salida (Stock < 0) es NULL.
        var movimientos = await LeerAsync(
            """
            SELECT "ProductoId", "TipoMovimiento", "Cantidad", "CantidadRestante", "UnidadMedidaId", "TipoOrigen", "ClaveOrigen", "AlmacenId"
            FROM "MovimientosProducto" WHERE "ClaveOrigen" = 'MIGRACION-STOCK'
            """);
        var movPorProducto = movimientos.ToDictionary(f => (Guid)f[0]!, f => f);
        Assert.Equal(120m, movPorProducto[ProductoPositivo][2]);
        Assert.Equal(120m, movPorProducto[ProductoPositivo][3]);
        Assert.Equal((short)3, movPorProducto[ProductoPositivo][1]); // AjustePositivo
        Assert.Equal(-5m, movPorProducto[ProductoNegativo][2]);
        Assert.Null(movPorProducto[ProductoNegativo][3]);
        Assert.Equal((short)4, movPorProducto[ProductoNegativo][1]); // AjusteNegativo
        Assert.Equal((short)99, movPorProducto[ProductoPositivo][5]); // TipoOrigen = Migracion
        Assert.Equal(Guid.Parse(AlmacenPrincipalSemilla), movPorProducto[ProductoPositivo][7]);
        // El movimiento del producto con unidad base KG usa la unidad KG (no la UND por defecto).
        Assert.Equal(Guid.Parse(UnidadKgSemilla), movPorProducto[ProductoKg][4]);
        Assert.False(movPorProducto.ContainsKey(ProductoCero));

        // 4. Ruling AR: CostoAjustado = false SOLO en los de Stock negativo; los de Stock positivo/cero conservan el suyo (true).
        var ajustados = await LeerCostoAjustadoAsync();
        Assert.True(ajustados[ProductoPositivo]);
        Assert.True(ajustados[ProductoCero]);
        Assert.False(ajustados[ProductoNegativo]);
        Assert.True(ajustados[ProductoBorrado]);
        Assert.True(ajustados[ProductoKg]);

        // 5. La columna "Stock" ya no existe.
        var columnas = (await LeerAsync(
            """SELECT column_name FROM information_schema.columns WHERE table_name = 'Productos' AND table_schema = current_schema()"""))
            .Select(c => (string)c[0]!).ToList();
        Assert.DoesNotContain("Stock", columnas);

        // 6. Down(): recompone Stock (redondeado a entero) SOLO a partir de los movimientos TipoOrigen = 99 insertados.
        await migrador.MigrateAsync(MigracionAnterior);
        var abajo = await LeerStockAsync();
        Assert.Equal(120, abajo[ProductoPositivo]);
        Assert.Equal(0, abajo[ProductoCero]);
        Assert.Equal(-5, abajo[ProductoNegativo]);
        Assert.Equal(7, abajo[ProductoBorrado]);
        Assert.Equal(15, abajo[ProductoKg]);

        // 7. Up() otra vez: idempotente en resultado (no duplica movimientos NI su valor, no dobla la existencia derivada).
        // Corrección 1: antes de la CTE, el INSERT de MovimientosValor tomaba TODOS los movimientos con
        // ClaveOrigen = 'MIGRACION-STOCK' (incluidos los de la pasada anterior) y duplicaba la fila de valor aunque el
        // NOT EXISTS ya evitara duplicar el movimiento de cantidad — este bloque prueba justo ese escenario.
        await migrador.MigrateAsync();
        var existenciasOtraVez = await LeerExistenciasAsync();
        Assert.Equal(120m, existenciasOtraVez.GetValueOrDefault(ProductoPositivo));
        Assert.Equal(-5m, existenciasOtraVez.GetValueOrDefault(ProductoNegativo));
        Assert.Equal(7m, existenciasOtraVez.GetValueOrDefault(ProductoBorrado));
        Assert.Equal(15m, existenciasOtraVez.GetValueOrDefault(ProductoKg));

        var conteoMovimientos = await LeerAsync(
            """SELECT COUNT(*) FROM "MovimientosProducto" WHERE "ClaveOrigen" = 'MIGRACION-STOCK' AND "ProductoId" = @p""",
            new { p = ProductoPositivo });
        Assert.Equal(1L, conteoMovimientos[0][0]);

        // Ni una sola fila de valor se duplicó: sigue habiendo exactamente una por producto migrado.
        var conteosValor = await LeerAsync(
            """SELECT "ProductoId", COUNT(*) FROM "MovimientosValor" WHERE "ClaveOrigen" = 'MIGRACION-STOCK' GROUP BY "ProductoId" """);
        var conteoValorPorProducto = conteosValor.ToDictionary(f => (Guid)f[0]!, f => (long)f[1]!);
        Assert.Equal(1L, conteoValorPorProducto[ProductoPositivo]);
        Assert.Equal(1L, conteoValorPorProducto[ProductoNegativo]);
        Assert.Equal(1L, conteoValorPorProducto[ProductoBorrado]);
        Assert.Equal(1L, conteoValorPorProducto[ProductoKg]);
        Assert.False(conteoValorPorProducto.ContainsKey(ProductoCero));

        // Y el SUM(ImporteCosto) sigue siendo el mismo que tras el primer Up() (no se dobló a 600/−40/84/37.50).
        var valoresOtraVez = await LeerAsync(
            """SELECT "ProductoId", SUM("ImporteCosto") FROM "MovimientosValor" WHERE "ClaveOrigen" = 'MIGRACION-STOCK' GROUP BY "ProductoId" """);
        var valorPorProductoOtraVez = valoresOtraVez.ToDictionary(f => (Guid)f[0]!, f => (decimal)f[1]!);
        Assert.Equal(300.0000m, valorPorProductoOtraVez[ProductoPositivo]);
        Assert.Equal(-20.0000m, valorPorProductoOtraVez[ProductoNegativo]);
        Assert.Equal(42.0000m, valorPorProductoOtraVez[ProductoBorrado]);
        Assert.Equal(18.7500m, valorPorProductoOtraVez[ProductoKg]);
    }

    private async Task<IMigrator> PrepararAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.AppConnectionString).Options;
        var contexto = new ApplicationDbContext(options);
        var migrador = contexto.GetService<IMigrator>();

        // Estado de partida idéntico en cada prueba: esquema anterior a ReemplazarStockPorLibro, sin productos ni movimientos.
        await migrador.MigrateAsync(MigracionAnterior);
        await EjecutarAsync("""
            DELETE FROM "AplicacionesMovimientoProducto";
            DELETE FROM "MovimientosValor";
            DELETE FROM "MovimientosProducto";
            DELETE FROM "UnidadesMedidaProducto";
            DELETE FROM "Productos";
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

    private async Task<List<object?[]>> LeerAsync(string sql, object? parametros = null)
    {
        await using var conexion = new NpgsqlConnection(fixture.AppConnectionString);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, conexion);
        if (parametros is not null)
        {
            foreach (var prop in parametros.GetType().GetProperties())
            {
                comando.Parameters.AddWithValue("@" + prop.Name, prop.GetValue(parametros) ?? DBNull.Value);
            }
        }

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

    private async Task<Dictionary<Guid, decimal>> LeerExistenciasAsync()
    {
        var filas = await LeerAsync("""SELECT "ProductoId", SUM("Cantidad") FROM "MovimientosProducto" GROUP BY "ProductoId" """);
        return filas.ToDictionary(f => (Guid)f[0]!, f => (decimal)f[1]!);
    }

    private async Task<Dictionary<Guid, bool>> LeerCostoAjustadoAsync()
    {
        var filas = await LeerAsync("""SELECT "Id", "CostoAjustado" FROM "Productos" """);
        return filas.ToDictionary(f => (Guid)f[0]!, f => (bool)f[1]!);
    }

    private async Task<Dictionary<Guid, int>> LeerStockAsync()
    {
        var filas = await LeerAsync("""SELECT "Id", "Stock" FROM "Productos" """);
        return filas.ToDictionary(f => (Guid)f[0]!, f => (int)f[1]!);
    }
}
