using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OpenSource1.Core.Entities;
using OpenSource1.Infrastructure.Data;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Infrastructure;

/// <summary>
/// Migración de datos <c>RecalcularCostoUnitario</c> (Task 8.3) contra Postgres real: parte del esquema anterior con
/// proyecciones <c>Productos."CostoUnitario"</c> desactualizadas y comprueba que Up() las deja en <c>V / Q</c> (4 decimales)
/// sobre todo el libro de valor si <c>Q &gt; 0</c>, conserva el valor si <c>Q &lt;= 0</c>, si el producto no tiene
/// movimientos o si el promedio sería negativo, recalcula también un producto borrado lógicamente, no toca (ni su <c>xmin</c>) la fila ya correcta, no escribe en los libros y
/// que Down() es un no-op documentado (Up() otra vez es idempotente). REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RecalcularCostoUnitarioMigrationTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    private const string MigracionAnterior = "20260927130329_RetirarEntradasYAppSettings";
    private static readonly Guid CategoriaGeneral = Guid.Parse("c1000000-0000-0000-0000-000000000001");
    private static readonly Guid UnidadUnd = Guid.Parse("a1000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Up_RecalculaLaProyeccionDeLosProductosConMovimientos_SinTocarLosLibros_YDownEsNoOp()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.AppConnectionString).Options;
        await using var contexto = new ApplicationDbContext(options);
        var migrador = contexto.GetService<IMigrator>();
        await using var conexion = new NpgsqlConnection(fixture.AppConnectionString);

        try
        {
            await migrador.MigrateAsync(MigracionAnterior);

            // (costo guardado, movimientos de valor (importe, cantidad)) → esperado tras Up().
            var dosCostos = await SembrarAsync(conexion, 1m, (100m, 10m), (200m, 10m));                 // 300 / 20 = 15
            var redondeo = await SembrarAsync(conexion, 0m, (3.3333m, 1m), (3.3333m, 1m), (3.3334m, 1m)); // 10 / 3 → 3.3333
            var mitad = await SembrarAsync(conexion, 0m, (1.0001m, 2m));                             // 0.50005 → 0.5001 (half away from zero)
            var conAjuste = await SembrarAsync(conexion, 2m, (100m, 10m), (-40m, -4m), (-5m, 0m));   // 55 / 6 → 9.1667
            var sinExistencia = await SembrarAsync(conexion, 9m, (20m, 5m), (-20m, -5m));            // Q = 0: conserva 9
            var negativa = await SembrarAsync(conexion, 4m, (-35m, -5m));                             // Q < 0: conserva 4
            var valorNegativo = await SembrarAsync(conexion, 6m, (10m, 2m), (-30m, 0m));              // −10 / 2 < 0: conserva 6
            var sinMovimientos = await SembrarAsync(conexion, 7.5m);                                   // conserva 7.5
            var yaCorrecto = await SembrarAsync(conexion, 12.5m, (25m, 2m));                          // 12.5: sin UPDATE
            var borrado = await SembrarAsync(conexion, 1m, (30m, 4m));                                // borrado lógico: 7.5
            await conexion.ExecuteAsync("""UPDATE "Productos" SET "IsDeleted" = true WHERE "Id" = @borrado""", new { borrado });

            var xminCorrecto = await XminAsync(conexion, yaCorrecto);
            var libroAntes = await LibroAsync(conexion);

            await migrador.MigrateAsync();

            var esperado = new Dictionary<Guid, decimal>
            {
                [dosCostos] = 15m,
                [redondeo] = 3.3333m,
                [mitad] = 0.5001m,
                [conAjuste] = 9.1667m,
                [sinExistencia] = 9m,
                [negativa] = 4m,
                [valorNegativo] = 6m,
                [sinMovimientos] = 7.5m,
                [yaCorrecto] = 12.5m,
                [borrado] = 7.5m,
            };
            await ComprobarAsync();
            Assert.Equal(xminCorrecto, await XminAsync(conexion, yaCorrecto));
            Assert.Equal(libroAntes, await LibroAsync(conexion));

            // Down() no revierte (no-op documentado) y Up() otra vez deja lo mismo.
            await migrador.MigrateAsync(MigracionAnterior);
            await ComprobarAsync();
            await migrador.MigrateAsync();
            await ComprobarAsync();

            async Task ComprobarAsync()
            {
                foreach (var (producto, costo) in esperado)
                {
                    Assert.Equal(costo, await conexion.ExecuteScalarAsync<decimal>(
                        """SELECT "CostoUnitario" FROM "Productos" WHERE "Id" = @producto""", new { producto }));
                }
            }
        }
        finally
        {
            await migrador.MigrateAsync();
        }
    }

    /// <summary>
    /// Producto con <paramref name="costoUnitario"/> guardado y sus movimientos de valor insertados directamente (el esquema
    /// del paso anterior es idéntico al actual: la migración es solo de datos).
    /// </summary>
    private async Task<Guid> SembrarAsync(NpgsqlConnection conexion, decimal costoUnitario, params (decimal Importe, decimal Cantidad)[] valores)
    {
        await using (var contexto = new ApplicationDbContext(
                         new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.AppConnectionString).Options))
        {
            var producto = new Producto
            {
                Codigo = $"RCU{Guid.NewGuid():N}"[..20],
                Nombre = "Producto de la migración RecalcularCostoUnitario",
                PrecioVenta = 1m,
                CategoriaId = CategoriaGeneral,
                UnidadMedidaBaseId = UnidadUnd,
                CostoUnitario = costoUnitario,
                CreatedBy = "test",
            };
            contexto.Productos.Add(producto);
            await contexto.SaveChangesAsync();

            var almacen = await conexion.ExecuteScalarAsync<Guid>("""SELECT "Id" FROM "Almacenes" WHERE "Codigo" = 'PRINCIPAL'""");
            var i = 0;
            foreach (var (importe, cantidad) in valores)
            {
                i++;
                await conexion.ExecuteAsync(
                    """
                    INSERT INTO "MovimientosValor" (
                        "MovimientoProductoId", "ProductoId", "AlmacenId", "TipoValor", "TipoMovimiento", "FechaRegistro",
                        "CantidadValorada", "CantidadFacturada", "ImporteCosto", "CostoPorUnidad", "ImporteVenta",
                        "ImporteCostoPosteadoContabilidad", "Ajuste", "TipoDocumento", "NumeroLineaDocumento",
                        "TipoOrigen", "ClaveOrigen", "CreatedAtUtc", "CreatedBy")
                    VALUES (NULL, @producto, @almacen, 1, 4, DATE '2026-01-01' + @i, @cantidad, 0, @importe, 0, 0, 0, false, 1, 1,
                            99, @clave, now(), 'test')
                    """,
                    new { producto = producto.Id, almacen, i, cantidad, importe, clave = $"RCU-{Guid.NewGuid():N}" });
            }

            return producto.Id;
        }
    }

    private static async Task<string> XminAsync(NpgsqlConnection conexion, Guid producto) =>
        await conexion.ExecuteScalarAsync<string>("""SELECT xmin::text FROM "Productos" WHERE "Id" = @producto""", new { producto })
        ?? string.Empty;

    /// <summary>Huella de los libros de inventario: la migración solo los agrega, no los modifica.</summary>
    private static Task<string?> LibroAsync(NpgsqlConnection conexion) =>
        conexion.ExecuteScalarAsync<string?>(
            """
            SELECT md5(COALESCE((SELECT string_agg(to_jsonb(v)::text, '|' ORDER BY v."Id") FROM "MovimientosValor" v), '')
                    || COALESCE((SELECT string_agg(to_jsonb(m)::text, '|' ORDER BY m."Id") FROM "MovimientosProducto" m), ''))
            """);
}
