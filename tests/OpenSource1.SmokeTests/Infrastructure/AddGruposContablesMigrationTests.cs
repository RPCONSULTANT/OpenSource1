using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Infrastructure.Data;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Infrastructure;

/// <summary>
/// Migración con datos <c>AddGruposContables</c> (Task 5.3) contra Postgres real: parte del esquema anterior con productos y
/// socios ya existentes (vivos y borrados lógicamente), aplica la migración y comprueba que todos quedan con los grupos
/// semilla por defecto (BIENES / ITBIS18 / GENERAL y NACIONAL / ITBIS18 / GENERAL), y que Down()/Up() es reversible.
/// REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AddGruposContablesMigrationTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    private const string MigracionAnterior = "20260926012226_AddPlanCuentas";
    private const string CategoriaGeneralSemilla = "c1000000-0000-0000-0000-000000000001";
    private const string UnidadUndSemilla = "a1000000-0000-0000-0000-000000000001";

    private static readonly Guid ProductoVivo = Guid.Parse("61111111-1111-1111-1111-111111111111");
    private static readonly Guid ProductoBorrado = Guid.Parse("62222222-2222-2222-2222-222222222222");
    private static readonly Guid SocioVivo = Guid.Parse("71111111-1111-1111-1111-111111111111");
    private static readonly Guid SocioBorrado = Guid.Parse("72222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Up_AsignaLosGruposPorDefectoALosProductosYSociosExistentes_YDownUpEsReversible()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.AppConnectionString).Options;
        await using var contexto = new ApplicationDbContext(options);
        var migrador = contexto.GetService<IMigrator>();

        await migrador.MigrateAsync(MigracionAnterior);
        await EjecutarAsync($"""
            INSERT INTO "Productos" ("Id","Codigo","Nombre","PrecioVenta","CategoriaId","UnidadMedidaBaseId",
                "MetodoCosteo","CostoUnitario","CostoEstandar","CostoAjustado","Bloqueado","CreatedAtUtc","CreatedBy","IsDeleted") VALUES
            ('{ProductoVivo}','MIG-P1','Vivo',1,'{CategoriaGeneralSemilla}','{UnidadUndSemilla}',1,0,0,true,0,'2026-01-01 00:00:00+00','test',false),
            ('{ProductoBorrado}','MIG-P2','Borrado',1,'{CategoriaGeneralSemilla}','{UnidadUndSemilla}',1,0,0,true,0,'2026-01-01 00:00:00+00','test',true);
            INSERT INTO "SociosNegocio" ("Id","Codigo","Tipo","NombreComercial","TipoDocumentoFiscal","LimiteCredito","Bloqueado",
                "CreatedAtUtc","CreatedBy","IsDeleted") VALUES
            ('{SocioVivo}','MIG001',1,'Socio vivo',0,0,0,'2026-01-01 00:00:00+00','test',false),
            ('{SocioBorrado}','MIG002',1,'Socio borrado',0,0,0,'2026-01-01 00:00:00+00','test',true);
            """);

        await migrador.MigrateAsync();
        await ComprobarGruposPorDefectoAsync();

        // Down() elimina columnas y tablas sin error (hay FK de productos/socios a los grupos); Up() otra vez vuelve a rellenar.
        await migrador.MigrateAsync(MigracionAnterior);
        var columnas = (await LeerAsync(
            """SELECT column_name FROM information_schema.columns WHERE table_name = 'Productos' AND table_schema = current_schema()"""))
            .Select(c => (string)c[0]!).ToList();
        Assert.DoesNotContain("GrupoProductoId", columnas);

        await migrador.MigrateAsync();
        await ComprobarGruposPorDefectoAsync();
    }

    private async Task ComprobarGruposPorDefectoAsync()
    {
        var productos = await LeerAsync(
            """SELECT "Id", "GrupoProductoId", "GrupoIvaProductoId", "GrupoInventarioId" FROM "Productos" WHERE "Id" IN (@a, @b)""",
            new { a = ProductoVivo, b = ProductoBorrado });
        Assert.Equal(2, productos.Count);
        foreach (var fila in productos)
        {
            Assert.Equal(GrupoContableIds.ProductoBienes, fila[1]);
            Assert.Equal(GrupoContableIds.IvaProductoItbis18, fila[2]);
            Assert.Equal(GrupoContableIds.InventarioGeneral, fila[3]);
        }

        var socios = await LeerAsync(
            """SELECT "Id", "GrupoNegocioId", "GrupoIvaNegocioId", "GrupoClienteContableId" FROM "SociosNegocio" WHERE "Id" IN (@a, @b)""",
            new { a = SocioVivo, b = SocioBorrado });
        Assert.Equal(2, socios.Count);
        foreach (var fila in socios)
        {
            Assert.Equal(GrupoContableIds.NegocioNacional, fila[1]);
            Assert.Equal(GrupoContableIds.IvaNegocioItbis18, fila[2]);
            Assert.Equal(GrupoContableIds.ClienteContableGeneral, fila[3]);
        }

        // Los grupos por defecto son los de la semilla (con esos códigos), no filas creadas por el backfill.
        var codigos = await LeerAsync(
            """
            SELECT (SELECT "Codigo" FROM "GruposProducto" WHERE "Id" = @p),
                   (SELECT "Codigo" FROM "GruposNegocio" WHERE "Id" = @n),
                   (SELECT "Codigo" FROM "GruposClienteContable" WHERE "Id" = @c)
            """,
            new { p = GrupoContableIds.ProductoBienes, n = GrupoContableIds.NegocioNacional, c = GrupoContableIds.ClienteContableGeneral });
        Assert.Equal(["BIENES", "NACIONAL", "GENERAL"], codigos[0]);
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
}
