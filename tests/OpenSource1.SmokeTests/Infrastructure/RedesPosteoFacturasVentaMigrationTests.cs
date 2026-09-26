using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;
using OpenSource1.Infrastructure.Data;
using OpenSource1.SmokeTests.TestInfrastructure;
using static OpenSource1.SmokeTests.TestInfrastructure.LibroClientesSemilla;

namespace OpenSource1.SmokeTests.Infrastructure;

/// <summary>
/// Migración <c>RedesPosteoFacturasVenta</c> (Task 6.4) contra Postgres real: redes de seguridad de lo que escribe el posteo.
/// <list type="bullet">
/// <item>CHECK: una línea de Producto posteada exige <c>MovimientoProductoId</c>.</item>
/// <item>CHECK: los totales de la factura van redondeados a 2.</item>
/// <item>Único <c>FacturasVenta.RegistroContableId</c> (NULL admite varias).</item>
/// <item>Único <c>MovimientosCliente (TipoDocumento, NumeroDocumento)</c>.</item>
/// </list>
/// Down() las quita (vuelven a admitirse esas filas) y Up() las repone. REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RedesPosteoFacturasVentaMigrationTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    private const string MigracionAnterior = "20260926210308_AddFacturasVentaYLibroClientes";
    private static readonly DateOnly Fecha = new(2026, 9, 10);

    [Fact]
    public async Task RedesDeSeguridad_RechazanLoQueElPosteoNuncaEscribe_YDownUpEsReversible()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.AppConnectionString).Options;
        await using var contexto = new ApplicationDbContext(options);
        var migrador = contexto.GetService<IMigrator>();
        await migrador.MigrateAsync();

        var producto = new Producto
        {
            Codigo = $"RED{Guid.NewGuid():N}"[..20],
            Nombre = "Producto de las redes de seguridad",
            CategoriaId = Guid.Parse("c1000000-0000-0000-0000-000000000001"),
            UnidadMedidaBaseId = LibroInventarioPrueba.UnidadUnd,
            CreatedBy = "test",
        };
        contexto.Productos.Add(producto);
        await contexto.SaveChangesAsync();

        await using var conexion = new NpgsqlConnection(fixture.AppConnectionString);
        var (numero, registro) = await SembrarAsync(conexion, "R1");

        await AssertSqlStateAsync(conexion, "23514", LineaProductoSinMovimiento, numero, registro);
        await AssertSqlStateAsync(conexion, "23514", FacturaConTotalesSinRedondear("X1", "XB1"), numero, registro);
        await AssertSqlStateAsync(conexion, "23505", FacturaConMismoAsiento("X2", "XB2"), numero, registro);
        await AssertSqlStateAsync(conexion, "23505", MovimientoClienteRepetido, numero, registro);

        // NULL en RegistroContableId no choca (índice único de PostgreSQL: los NULL son distintos).
        await InsertarFacturaAsync(conexion, "RNULL1", await InsertarSocioAsync(conexion, "Nulo 1"), Fecha, [], [new LineaIva("EXENTO", 0m, 1m, 0m)], null);
        await InsertarFacturaAsync(conexion, "RNULL2", await InsertarSocioAsync(conexion, "Nulo 2"), Fecha, [], [new LineaIva("EXENTO", 0m, 1m, 0m)], null);

        var indices = (await conexion.QueryAsync<string>(
            "SELECT indexdef FROM pg_indexes WHERE indexname IN ('IX_FacturasVenta_RegistroContableId', 'IX_MovimientosCliente_TipoDocumento_NumeroDocumento')")).ToList();
        Assert.Equal(2, indices.Count);
        Assert.All(indices, d => Assert.StartsWith("CREATE UNIQUE INDEX", d));

        // Down(): las cuatro filas se vuelven a admitir (y los índices siguen existiendo, no únicos).
        await migrador.MigrateAsync(MigracionAnterior);
        await conexion.ExecuteAsync(LineaProductoSinMovimiento, new { Numero = numero, Registro = registro });
        await conexion.ExecuteAsync(FacturaConTotalesSinRedondear("Y1", "YB1"), new { Numero = numero, Registro = registro });
        await conexion.ExecuteAsync(FacturaConMismoAsiento("Y2", "YB2"), new { Numero = numero, Registro = registro });
        await conexion.ExecuteAsync(MovimientoClienteRepetido, new { Numero = numero, Registro = registro });
        Assert.Equal(2L, await conexion.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM pg_indexes WHERE indexname IN ('IX_FacturasVenta_RegistroContableId', 'IX_MovimientosCliente_TipoDocumento_NumeroDocumento') AND indexdef NOT LIKE 'CREATE UNIQUE%'"));

        // Up() otra vez: con esas filas en las tablas (append-only, no se pueden borrar) no podría crear los índices únicos, así
        // que se baja hasta antes de las tablas del documento posteado (que se eliminan con ellas) y se vuelve a subir todo.
        await migrador.MigrateAsync("20260926202935_AddFacturasVentaBorrador");
        await migrador.MigrateAsync();
        var (numero2, registro2) = await SembrarAsync(conexion, "R2");
        await AssertSqlStateAsync(conexion, "23514", LineaProductoSinMovimiento, numero2, registro2);
        await AssertSqlStateAsync(conexion, "23514", FacturaConTotalesSinRedondear("Z1", "ZB1"), numero2, registro2);
        await AssertSqlStateAsync(conexion, "23505", FacturaConMismoAsiento("Z2", "ZB2"), numero2, registro2);
        await AssertSqlStateAsync(conexion, "23505", MovimientoClienteRepetido, numero2, registro2);
    }

    private static async Task<(string Numero, long Registro)> SembrarAsync(NpgsqlConnection conexion, string numero)
    {
        var socio = await InsertarSocioAsync(conexion, "Redes");
        var registro = await InsertarRegistroContableAsync(conexion, numero);
        await InsertarFacturaAsync(conexion, numero, socio, Fecha,
            [new Linea(10000, TipoLineaFactura.CuentaContable, 10m, CuentaContableId: CuentaContableIds.Ventas)],
            [new LineaIva("ITBIS18", 18m, 10m, 1.8m)],
            registro);
        await InsertarMovimientoAsync(conexion, socio, TipoDocumentoCliente.Factura, numero, Fecha, 11.8m);
        return (numero, registro);
    }

    private const string LineaProductoSinMovimiento = """
        INSERT INTO "LineasFacturaVenta" ("FacturaVentaNumero","NumeroLinea","Tipo","ProductoId","Descripcion","CantidadPorUnidadMedida",
            "Cantidad","PrecioUnitario","PorcentajeDescuentoLinea","ImporteDescuentoLinea","ImporteLinea","PorcentajeIva","MovimientoProductoId")
        SELECT @Numero, 90000, 1, p."Id", 'Producto sin salida', 1, 1, 1, 0, 0, 1, 18, NULL
        FROM "Productos" p WHERE p."Codigo" LIKE 'RED%' ORDER BY p."Id" LIMIT 1
        """;

    private static string FacturaConTotalesSinRedondear(string prefijo, string prefijoBorrador) => $"""
        INSERT INTO "FacturasVenta" ("Numero","NumeroBorrador","SocioNegocioId","SocioNegocioFacturarAId","NombreFacturacion",
            "TipoDocumentoFiscal","FechaRegistro","FechaDocumento","FechaVencimiento","GrupoNegocioId","GrupoIvaNegocioId",
            "GrupoClienteContableId","AlmacenId","Moneda","ImporteSinIva","ImporteIva","ImporteTotal","CreatedAtUtc","CreatedBy")
        SELECT '{prefijo}' || "Numero", '{prefijoBorrador}' || "NumeroBorrador", "SocioNegocioId", "SocioNegocioFacturarAId",
            "NombreFacturacion", "TipoDocumentoFiscal", "FechaRegistro", "FechaDocumento", "FechaVencimiento", "GrupoNegocioId",
            "GrupoIvaNegocioId", "GrupoClienteContableId", "AlmacenId", "Moneda", 10.005, 1.8, 11.805, now(), 'test'
        FROM "FacturasVenta" WHERE "Numero" = @Numero
        """;

    private static string FacturaConMismoAsiento(string prefijo, string prefijoBorrador) => $"""
        INSERT INTO "FacturasVenta" ("Numero","NumeroBorrador","SocioNegocioId","SocioNegocioFacturarAId","NombreFacturacion",
            "TipoDocumentoFiscal","FechaRegistro","FechaDocumento","FechaVencimiento","GrupoNegocioId","GrupoIvaNegocioId",
            "GrupoClienteContableId","AlmacenId","Moneda","ImporteSinIva","ImporteIva","ImporteTotal","RegistroContableId",
            "CreatedAtUtc","CreatedBy")
        SELECT '{prefijo}' || "Numero", '{prefijoBorrador}' || "NumeroBorrador", "SocioNegocioId", "SocioNegocioFacturarAId",
            "NombreFacturacion", "TipoDocumentoFiscal", "FechaRegistro", "FechaDocumento", "FechaVencimiento", "GrupoNegocioId",
            "GrupoIvaNegocioId", "GrupoClienteContableId", "AlmacenId", "Moneda", 1, 0, 1, @Registro, now(), 'test'
        FROM "FacturasVenta" WHERE "Numero" = @Numero
        """;

    private const string MovimientoClienteRepetido = """
        INSERT INTO "MovimientosCliente" ("SocioNegocioId","FechaRegistro","FechaDocumento","FechaVencimiento","TipoDocumento",
            "NumeroDocumento","ImporteOriginal","GrupoClienteContableId","CuentaCxCId","TipoOrigen","ClaveOrigen","CreatedAtUtc","CreatedBy")
        SELECT "SocioNegocioId", "FechaRegistro", "FechaDocumento", "FechaVencimiento", "TipoDocumento", "NumeroDocumento", 1,
            "GrupoClienteContableId", "CuentaCxCId", "TipoOrigen", 'OTRA', now(), 'test'
        FROM "MovimientosCliente" WHERE "TipoDocumento" = 1 AND "NumeroDocumento" = @Numero
        """;

    private static async Task AssertSqlStateAsync(NpgsqlConnection conexion, string sqlState, string sql, string numero, long registro)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => conexion.ExecuteAsync(sql, new { Numero = numero, Registro = registro }));
        Assert.True(error.SqlState == sqlState, $"Se esperaba {sqlState} y llegó {error.SqlState}: {error.MessageText}\n{sql}");
    }
}
