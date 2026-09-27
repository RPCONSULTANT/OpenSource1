using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;
using OpenSource1.Infrastructure.Data;
using OpenSource1.SmokeTests.TestInfrastructure;
using static OpenSource1.SmokeTests.TestInfrastructure.LibroClientesSemilla;

namespace OpenSource1.SmokeTests.Infrastructure;

/// <summary>
/// Migración <c>AddFacturasVentaYLibroClientes</c> (Task 6.3) contra Postgres real: las cinco tablas del documento posteado y del
/// libro de clientes admiten INSERT pero rechazan UPDATE, DELETE y TRUNCATE (trigger <c>libro_inventario_append_only()</c>,
/// SQLSTATE P0001); índices del brief; CHECK, únicos y FK; y Down()/Up() reversible. REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AddFacturasVentaYLibroClientesMigrationTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    private const string MigracionAnterior = "20260926202935_AddFacturasVentaBorrador";
    private static readonly DateOnly Fecha = new(2026, 9, 10);

    private static readonly string[] Tablas =
        ["FacturasVenta", "LineasFacturaVenta", "LineasIvaFacturaVenta", "MovimientosCliente", "MovimientosClienteDetalle"];

    [Fact]
    public async Task LasCincoTablas_SonAppendOnly_ConIndicesYRestricciones_YDownUpEsReversible()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.AppConnectionString).Options;
        await using var contexto = new ApplicationDbContext(options);
        var migrador = contexto.GetService<IMigrator>();
        await migrador.MigrateAsync();

        await using var conexion = new NpgsqlConnection(fixture.AppConnectionString);
        await ComprobarAsync(conexion, "00000001");

        // Down(): sin tablas ni triggers; Up() otra vez: mismo comportamiento.
        await migrador.MigrateAsync(MigracionAnterior);
        Assert.Equal(0L, await conexion.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = ANY(@Tablas)", new { Tablas }));
        Assert.Equal(0L, await ContarTriggersAsync(conexion));
        await migrador.MigrateAsync();
        await ComprobarAsync(conexion, "00000002");
    }

    private static async Task ComprobarAsync(NpgsqlConnection conexion, string numero)
    {
        // 2 triggers por tabla (fila UPDATE/DELETE + sentencia TRUNCATE), habilitados.
        Assert.Equal(10L, await ContarTriggersAsync(conexion));

        // INSERT permitido en las cinco.
        var socio = await InsertarSocioAsync(conexion, "Migración");
        var registro = await InsertarRegistroContableAsync(conexion, numero);
        await InsertarFacturaAsync(conexion, numero, socio, Fecha,
            [
                new Linea(10000, TipoLineaFactura.CuentaContable, 10.03m, CuentaContableId: CuentaContableIds.Ventas),
                new Linea(20000, TipoLineaFactura.Comentario, 0m, Descripcion: "Nota"),
            ],
            [new LineaIva("ITBIS18", 18m, 10.03m, 1.81m)],
            registro);
        var factura = await InsertarMovimientoAsync(conexion, socio, TipoDocumentoCliente.Factura, numero, Fecha, 11.84m);
        var pago = await InsertarMovimientoAsync(conexion, socio, TipoDocumentoCliente.Pago, $"P{numero}", Fecha, -5m, TipoOrigenMovimiento.Cobro);
        await AplicarAsync(conexion, factura, pago, 5m, Fecha);

        // UPDATE / DELETE / TRUNCATE rechazados en cada tabla con P0001.
        var sentencias = new (string Sql, string? Tabla)[]
        {
            ("""UPDATE "FacturasVenta" SET "ImporteTotal" = 0, "ImporteSinIva" = 0, "ImporteIva" = 0 WHERE "Numero" = @Numero""", "FacturasVenta"),
            ("""UPDATE "FacturasVenta" SET "Descripcion" = 'x' WHERE "Numero" = @Numero""", "FacturasVenta"),
            ("""DELETE FROM "FacturasVenta" WHERE "Numero" = @Numero""", "FacturasVenta"),
            ("""UPDATE "LineasFacturaVenta" SET "ImporteLinea" = 0 WHERE "FacturaVentaNumero" = @Numero""", "LineasFacturaVenta"),
            ("""DELETE FROM "LineasFacturaVenta" WHERE "FacturaVentaNumero" = @Numero""", "LineasFacturaVenta"),
            ("""UPDATE "LineasIvaFacturaVenta" SET "ImporteIva" = 0 WHERE "FacturaVentaNumero" = @Numero""", "LineasIvaFacturaVenta"),
            ("""DELETE FROM "LineasIvaFacturaVenta" WHERE "FacturaVentaNumero" = @Numero""", "LineasIvaFacturaVenta"),
            ("""UPDATE "MovimientosCliente" SET "ImporteOriginal" = 0 WHERE "Id" = @Factura""", "MovimientosCliente"),
            ("""DELETE FROM "MovimientosCliente" WHERE "Id" = @Factura""", "MovimientosCliente"),
            ("""UPDATE "MovimientosClienteDetalle" SET "Importe" = 0 WHERE "MovimientoClienteId" = @Factura""", "MovimientosClienteDetalle"),
            ("""DELETE FROM "MovimientosClienteDetalle" WHERE "MovimientoClienteId" = @Factura""", "MovimientosClienteDetalle"),
            // Desde la Task 8.6 LineasNotaCreditoVenta(Borrador) referencia LineasFacturaVenta: sin CASCADE, Postgres lo rechaza antes
            // (0A000) por la FK; con CASCADE salta el trigger (de esta tabla o de la de notas, ambas append-only).
            ("""TRUNCATE "LineasFacturaVenta" CASCADE""", null),
            ("""TRUNCATE "LineasIvaFacturaVenta" """, "LineasIvaFacturaVenta"),
            ("""TRUNCATE "MovimientosClienteDetalle" """, "MovimientosClienteDetalle"),
            // Con CASCADE se truncan también las tablas hijas: salta el trigger de la primera, sea cual sea.
            ("""TRUNCATE "FacturasVenta" CASCADE""", null),
            ("""TRUNCATE "MovimientosCliente" CASCADE""", null),
        };
        foreach (var (sql, tabla) in sentencias)
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() =>
                conexion.ExecuteAsync(sql, new { Numero = numero, Factura = factura }));
            Assert.True(error.SqlState == "P0001", $"{sql}: {error.SqlState} {error.MessageText}");
            Assert.Contains(tabla ?? "es de solo inserción", error.MessageText);
        }

        // Nada cambió: saldo derivado = 11.84 − 5 (pago) − 5 + 5 (aplicación de signo opuesto en cada lado) = 6.84.
        Assert.Equal(6.84m, await conexion.ExecuteScalarAsync<decimal>(
            """
            SELECT SUM(d."Importe") FROM "MovimientosClienteDetalle" d
            JOIN "MovimientosCliente" m ON m."Id" = d."MovimientoClienteId" WHERE m."SocioNegocioId" = @Socio
            """, new { Socio = socio }));
        Assert.Equal(2L, await conexion.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM "LineasFacturaVenta" WHERE "FacturaVentaNumero" = @Numero""", new { Numero = numero }));

        // Índices del brief (y los únicos de soporte).
        var indices = (await conexion.QueryAsync<string>(
            "SELECT indexname FROM pg_indexes WHERE tablename = ANY(@Tablas)", new { Tablas })).ToHashSet();
        Assert.Superset(
            new HashSet<string>
            {
                "PK_FacturasVenta",
                "IX_FacturasVenta_NumeroBorrador",
                "IX_LineasFacturaVenta_FacturaVentaNumero_NumeroLinea",
                "IX_LineasIvaFacturaVenta_FacturaVentaNumero_IdentificadorIva",
                "IX_MovimientosCliente_SocioNegocioId_FechaRegistro",
                "IX_MovimientosCliente_TipoDocumento_NumeroDocumento",
                "IX_MovimientosCliente_TipoOrigen_ClaveOrigen",
                "IX_MovimientosClienteDetalle_MovimientoClienteId",
                "IX_MovimientosClienteDetalle_MovimientoClienteAplicadoId",
            },
            indices);

        // Restricciones: total incoherente, aplicación sin contraparte o contra sí misma (23514); borrador posteado dos veces e
        // IVA repetido (23505); FK (23503).
        await AssertSqlStateAsync(conexion, "23514", """
            INSERT INTO "FacturasVenta" ("Numero","NumeroBorrador","SocioNegocioId","SocioNegocioFacturarAId","NombreFacturacion",
                "TipoDocumentoFiscal","FechaRegistro","FechaDocumento","FechaVencimiento","GrupoNegocioId","GrupoIvaNegocioId",
                "GrupoClienteContableId","AlmacenId","Moneda","ImporteSinIva","ImporteIva","ImporteTotal","CreatedAtUtc","CreatedBy")
            SELECT 'X' || "Numero", 'X' || "NumeroBorrador", "SocioNegocioId", "SocioNegocioFacturarAId", "NombreFacturacion",
                "TipoDocumentoFiscal", "FechaRegistro", "FechaDocumento", "FechaVencimiento", "GrupoNegocioId", "GrupoIvaNegocioId",
                "GrupoClienteContableId", "AlmacenId", "Moneda", 10, 1.8, 11.81, now(), 'test'
            FROM "FacturasVenta" WHERE "Numero" = @Numero
            """, numero, factura);
        await AssertSqlStateAsync(conexion, "23505", """
            INSERT INTO "FacturasVenta" ("Numero","NumeroBorrador","SocioNegocioId","SocioNegocioFacturarAId","NombreFacturacion",
                "TipoDocumentoFiscal","FechaRegistro","FechaDocumento","FechaVencimiento","GrupoNegocioId","GrupoIvaNegocioId",
                "GrupoClienteContableId","AlmacenId","Moneda","ImporteSinIva","ImporteIva","ImporteTotal","CreatedAtUtc","CreatedBy")
            SELECT 'X' || "Numero", "NumeroBorrador", "SocioNegocioId", "SocioNegocioFacturarAId", "NombreFacturacion",
                "TipoDocumentoFiscal", "FechaRegistro", "FechaDocumento", "FechaVencimiento", "GrupoNegocioId", "GrupoIvaNegocioId",
                "GrupoClienteContableId", "AlmacenId", "Moneda", 0, 0, 0, now(), 'test'
            FROM "FacturasVenta" WHERE "Numero" = @Numero
            """, numero, factura);
        await AssertSqlStateAsync(conexion, "23505", $"""
            INSERT INTO "LineasIvaFacturaVenta" ("FacturaVentaNumero","IdentificadorIva","PorcentajeIva","BaseImponible","ImporteIva","CuentaIvaId")
            VALUES (@Numero, 'ITBIS18', 18, 1, 0.18, '{CuentaContableIds.IvaPorPagar}')
            """, numero, factura);
        await AssertSqlStateAsync(conexion, "23505", """
            INSERT INTO "LineasFacturaVenta" ("FacturaVentaNumero","NumeroLinea","Tipo","Descripcion","CantidadPorUnidadMedida",
                "Cantidad","PrecioUnitario","PorcentajeDescuentoLinea","ImporteDescuentoLinea","ImporteLinea","PorcentajeIva")
            VALUES (@Numero, 20000, 3, 'Repetida', 0, 0, 0, 0, 0, 0, 0)
            """, numero, factura);
        await AssertSqlStateAsync(conexion, "23514", """
            INSERT INTO "LineasFacturaVenta" ("FacturaVentaNumero","NumeroLinea","Tipo","Descripcion","CantidadPorUnidadMedida",
                "Cantidad","PrecioUnitario","PorcentajeDescuentoLinea","ImporteDescuentoLinea","ImporteLinea","PorcentajeIva")
            VALUES (@Numero, 30000, 1, 'Producto sin producto', 1, 1, 1, 0, 0, 1, 18)
            """, numero, factura);
        await AssertSqlStateAsync(conexion, "23514", """
            INSERT INTO "MovimientosClienteDetalle" ("MovimientoClienteId","TipoMovimiento","Importe","FechaRegistro",
                "MovimientoClienteAplicadoId","TipoOrigen","ClaveOrigen","CreatedAtUtc","CreatedBy")
            VALUES (@Factura, 3, -1, '2026-09-10', NULL, 5, 'X', now(), 'test')
            """, numero, factura);
        await AssertSqlStateAsync(conexion, "23514", """
            INSERT INTO "MovimientosClienteDetalle" ("MovimientoClienteId","TipoMovimiento","Importe","FechaRegistro",
                "MovimientoClienteAplicadoId","TipoOrigen","ClaveOrigen","CreatedAtUtc","CreatedBy")
            VALUES (@Factura, 3, -1, '2026-09-10', @Factura, 5, 'X', now(), 'test')
            """, numero, factura);
        await AssertSqlStateAsync(conexion, "23503", """
            INSERT INTO "MovimientosClienteDetalle" ("MovimientoClienteId","TipoMovimiento","Importe","FechaRegistro",
                "MovimientoClienteAplicadoId","TipoOrigen","ClaveOrigen","CreatedAtUtc","CreatedBy")
            VALUES (@Factura, 3, -1, '2026-09-10', 999999999, 5, 'X', now(), 'test')
            """, numero, factura);
        await AssertSqlStateAsync(conexion, "23503", """
            INSERT INTO "LineasIvaFacturaVenta" ("FacturaVentaNumero","IdentificadorIva","PorcentajeIva","BaseImponible","ImporteIva","CuentaIvaId")
            VALUES ('NOEXISTE', 'ITBIS18', 18, 1, 0.18, gen_random_uuid())
            """, numero, factura);

        // El Id es identidad ALWAYS: no se puede fijar a mano.
        await AssertSqlStateAsync(conexion, "428C9", """
            INSERT INTO "MovimientosClienteDetalle" ("Id","MovimientoClienteId","TipoMovimiento","Importe","FechaRegistro",
                "TipoOrigen","ClaveOrigen","CreatedAtUtc","CreatedBy")
            VALUES (1, @Factura, 1, 1, '2026-09-10', 5, 'X', now(), 'test')
            """, numero, factura);
    }

    private static Task<long> ContarTriggersAsync(NpgsqlConnection conexion) =>
        conexion.ExecuteScalarAsync<long>(
            """
            SELECT COUNT(*) FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid
            WHERE NOT t.tgisinternal AND t.tgenabled = 'O' AND c.relname = ANY(@Tablas)
            """, new { Tablas });

    private static async Task AssertSqlStateAsync(NpgsqlConnection conexion, string sqlState, string sql, string numero, long factura)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => conexion.ExecuteAsync(sql, new { Numero = numero, Factura = factura }));
        Assert.True(error.SqlState == sqlState, $"Se esperaba {sqlState} y llegó {error.SqlState}: {error.MessageText}\n{sql}");
    }
}
