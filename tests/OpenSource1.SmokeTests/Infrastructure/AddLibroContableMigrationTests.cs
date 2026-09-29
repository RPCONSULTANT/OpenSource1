using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Infrastructure.Data;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Infrastructure;

/// <summary>
/// Migración con datos <c>AddLibroContable</c> (Task 5.5) contra Postgres real: parte del esquema anterior con movimientos de
/// valor ya escritos (sin grupos, con uuids basura sin catálogo y con grupos válidos), aplica la migración y comprueba el
/// backfill (grupos del producto o semillas; negocio del socio o NACIONAL; sin socio, NULL), que las FK existen, que el
/// trigger append-only de <c>MovimientosValor</c> vuelve a estar ACTIVO (la desactivación es solo dentro de la migración), la
/// serie <c>CONTAB</c> sembrada, y que Down()/Up() es reversible. REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AddLibroContableMigrationTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    private const string MigracionAnterior = "20260926032453_AddSetupsContables";
    private static readonly Guid CategoriaGeneral = Guid.Parse("c1000000-0000-0000-0000-000000000001");
    private static readonly Guid UnidadUnd = Guid.Parse("a1000000-0000-0000-0000-000000000001");

    private static readonly Guid ProductoConGrupos = Guid.Parse("63333333-3333-3333-3333-333333333333");
    private static readonly Guid ProductoSinGrupos = Guid.Parse("64444444-4444-4444-4444-444444444444");
    private static readonly Guid SocioExterior = Guid.Parse("73333333-3333-3333-3333-333333333333");
    private static readonly Guid SocioSinGrupo = Guid.Parse("74444444-4444-4444-4444-444444444444");
    private static readonly Guid GrupoInventarioPropio = Guid.Parse("83333333-3333-3333-3333-333333333333");
    private static readonly Guid Basura = Guid.Parse("99999999-9999-9999-9999-999999999999");

    [Fact]
    public async Task Up_RellenaLosGruposDeLosMovimientosDeValor_CreaFks_ReactivaElTrigger_YDownUpEsReversible()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.AppConnectionString).Options;
        await using var contexto = new ApplicationDbContext(options);
        var migrador = contexto.GetService<IMigrator>();

        await migrador.MigrateAsync(MigracionAnterior);

        await using var conexion = new NpgsqlConnection(fixture.AppConnectionString);
        await conexion.ExecuteAsync($"""
            INSERT INTO "GruposInventario" ("Id","Codigo","Descripcion","CreatedAtUtc","CreatedBy","IsDeleted")
            VALUES ('{GrupoInventarioPropio}','MIG-INV','Propio','2026-01-01 00:00:00+00','test',false);
            INSERT INTO "Productos" ("Id","Codigo","Nombre","PrecioVenta","CategoriaId","UnidadMedidaBaseId","MetodoCosteo",
                "CostoUnitario","CostoEstandar","CostoAjustado","Bloqueado","CreatedAtUtc","CreatedBy","IsDeleted",
                "GrupoProductoId","GrupoInventarioId") VALUES
            ('{ProductoConGrupos}','MIG-LC1','Con grupos',1,'{CategoriaGeneral}','{UnidadUnd}',1,0,0,true,0,'2026-01-01 00:00:00+00','test',false,
             '{GrupoContableIds.ProductoServicios}','{GrupoInventarioPropio}'),
            ('{ProductoSinGrupos}','MIG-LC2','Sin grupos',1,'{CategoriaGeneral}','{UnidadUnd}',1,0,0,true,0,'2026-01-01 00:00:00+00','test',false,
             NULL,NULL);
            INSERT INTO "SociosNegocio" ("Id","Codigo","Tipo","NombreComercial","TipoDocumentoFiscal","LimiteCredito","Bloqueado",
                "CreatedAtUtc","CreatedBy","IsDeleted","GrupoNegocioId") VALUES
            ('{SocioExterior}','MIGLC1',1,'Exterior',0,0,0,'2026-01-01 00:00:00+00','test',false,'{GrupoContableIds.NegocioExterior}'),
            ('{SocioSinGrupo}','MIGLC2',1,'Sin grupo',0,0,0,'2026-01-01 00:00:00+00','test',false,NULL);
            """);

        // (producto, socio, grupos ya escritos en la fila: inventario, producto, negocio)
        var sinGruposNiSocio = await InsertarMovimientoAsync(conexion, ProductoConGrupos, null, null, null, null);
        var socioSinGrupo = await InsertarMovimientoAsync(conexion, ProductoSinGrupos, SocioSinGrupo, null, null, null);
        var conBasura = await InsertarMovimientoAsync(conexion, ProductoConGrupos, SocioExterior, Basura, Basura, Basura);
        var basuraSinSocio = await InsertarMovimientoAsync(conexion, ProductoSinGrupos, null, null, null, Basura);
        var yaValidos = await InsertarMovimientoAsync(
            conexion, ProductoSinGrupos, null, GrupoContableIds.InventarioGeneral, GrupoContableIds.ProductoServicios, GrupoContableIds.NegocioExterior);

        await migrador.MigrateAsync();
        await ComprobarAsync();

        // Down() quita FK, tablas y la serie; Up() otra vez: el backfill es idempotente (todo ya es válido y se conserva).
        await migrador.MigrateAsync(MigracionAnterior);
        Assert.Equal(0L, await conexion.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM information_schema.tables WHERE table_name IN ('MovimientosContables', 'RegistrosContables')"""));
        Assert.Equal(0L, await conexion.ExecuteScalarAsync<long>("""SELECT COUNT(*) FROM "Series" WHERE "Codigo" = 'CONTAB'"""));
        await migrador.MigrateAsync();
        await ComprobarAsync();

        async Task ComprobarAsync()
        {
            Assert.Equal((GrupoInventarioPropio, GrupoContableIds.ProductoServicios, (Guid?)null), await GruposAsync(conexion, sinGruposNiSocio));
            Assert.Equal((GrupoContableIds.InventarioGeneral, GrupoContableIds.ProductoBienes, GrupoContableIds.NegocioNacional), await GruposAsync(conexion, socioSinGrupo));
            Assert.Equal((GrupoInventarioPropio, GrupoContableIds.ProductoServicios, GrupoContableIds.NegocioExterior), await GruposAsync(conexion, conBasura));
            Assert.Equal((GrupoContableIds.InventarioGeneral, GrupoContableIds.ProductoBienes, (Guid?)null), await GruposAsync(conexion, basuraSinSocio));
            Assert.Equal((GrupoContableIds.InventarioGeneral, GrupoContableIds.ProductoServicios, GrupoContableIds.NegocioExterior), await GruposAsync(conexion, yaValidos));

            // Trigger reactivado ('O' = enabled origin) y efectivo: ningún UPDATE sobre el libro de valor tras la migración.
            Assert.Equal('O', await conexion.ExecuteScalarAsync<char>(
                """SELECT tgenabled FROM pg_trigger WHERE tgname = 'TR_MovimientosValor_AppendOnly'"""));
            var error = await Assert.ThrowsAsync<PostgresException>(() => conexion.ExecuteAsync(
                """UPDATE "MovimientosValor" SET "GrupoNegocioId" = NULL WHERE "Id" = @Id""", new { Id = conBasura }));
            Assert.Equal("P0001", error.SqlState);

            // FK efectivas: un uuid sin catálogo ya no entra.
            var fk = await Assert.ThrowsAsync<PostgresException>(() => conexion.ExecuteAsync($"""
                INSERT INTO "MovimientosValor" ("ProductoId","AlmacenId","TipoValor","TipoMovimiento","FechaRegistro",
                    "CantidadValorada","CantidadFacturada","ImporteCosto","CostoPorUnidad","ImporteVenta",
                    "ImporteCostoPosteadoContabilidad","Ajuste","TipoDocumento","NumeroLineaDocumento","GrupoProductoId",
                    "TipoOrigen","ClaveOrigen","CreatedAtUtc","CreatedBy")
                VALUES ('{ProductoSinGrupos}','{AlmacenIds.Principal}',1,4,'2026-01-01',0,0,0,0,0,0,false,0,0,'{Basura}',99,'X',now(),'test')
                """));
            Assert.Equal("23503", fk.SqlState);

            // Serie CONTAB sembrada, sin números consumidos, y el libro contable vacío con su trigger.
            Assert.Equal("00000000", await conexion.ExecuteScalarAsync<string>(
                """SELECT "UltimoNumeroUsado" FROM "LineasSerie" WHERE "Id" = @Id""", new { Id = SerieContabilidadIds.LineaSerieId }));
            // 4 de append-only/truncate (AddLibroContable) + el constraint trigger de cuadre (VerificarCuadreLibroContable).
            Assert.Equal(5L, await conexion.ExecuteScalarAsync<long>(
                """SELECT COUNT(*) FROM pg_trigger WHERE tgname LIKE 'TR_MovimientosContables_%' OR tgname LIKE 'TR_RegistrosContables_%'"""));
        }
    }

    private static async Task<long> InsertarMovimientoAsync(
        NpgsqlConnection conexion, Guid productoId, Guid? socioId, Guid? grupoInventario, Guid? grupoProducto, Guid? grupoNegocio)
    {
        var movimientoProductoId = await conexion.ExecuteScalarAsync<long>(
            """
            INSERT INTO "MovimientosProducto" ("ProductoId","AlmacenId","TipoMovimiento","TipoDocumento","NumeroLineaDocumento",
                "FechaRegistro","FechaDocumento","Cantidad","CantidadRestante","CantidadFacturada","UnidadMedidaId",
                "CantidadPorUnidadMedida","SocioNegocioId","TipoOrigen","ClaveOrigen","CreatedAtUtc","CreatedBy")
            VALUES (@ProductoId,@AlmacenId,4,0,0,'2026-01-01','2026-01-01',1,1,0,@UnidadId,1,@SocioId,99,'MIG',now(),'test')
            RETURNING "Id"
            """,
            new { ProductoId = productoId, AlmacenId = AlmacenIds.Principal, UnidadId = UnidadUnd, SocioId = socioId });

        return await conexion.ExecuteScalarAsync<long>(
            """
            INSERT INTO "MovimientosValor" ("MovimientoProductoId","ProductoId","AlmacenId","TipoValor","TipoMovimiento",
                "FechaRegistro","CantidadValorada","CantidadFacturada","ImporteCosto","CostoPorUnidad","ImporteVenta",
                "ImporteCostoPosteadoContabilidad","Ajuste","TipoDocumento","NumeroLineaDocumento","GrupoInventarioId",
                "GrupoNegocioId","GrupoProductoId","TipoOrigen","ClaveOrigen","CreatedAtUtc","CreatedBy")
            VALUES (@MovimientoProductoId,@ProductoId,@AlmacenId,1,4,'2026-01-01',1,0,5,5,0,0,false,0,0,@GrupoInventario,
                @GrupoNegocio,@GrupoProducto,99,'MIG',now(),'test')
            RETURNING "Id"
            """,
            new
            {
                MovimientoProductoId = movimientoProductoId,
                ProductoId = productoId,
                AlmacenId = AlmacenIds.Principal,
                GrupoInventario = grupoInventario,
                GrupoNegocio = grupoNegocio,
                GrupoProducto = grupoProducto,
            });
    }

    private static async Task<(Guid? Inventario, Guid? Producto, Guid? Negocio)> GruposAsync(NpgsqlConnection conexion, long id) =>
        await conexion.QuerySingleAsync<(Guid?, Guid?, Guid?)>(
            """SELECT "GrupoInventarioId", "GrupoProductoId", "GrupoNegocioId" FROM "MovimientosValor" WHERE "Id" = @Id""",
            new { Id = id });
}
