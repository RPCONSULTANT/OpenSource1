using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OpenSource1.Core.Enums;
using OpenSource1.Infrastructure.Data;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Infrastructure;

/// <summary>
/// Migración <c>BorradoresEnlaceUnico</c>: respaldo en BD del doble posteo. Un documento posteado (factura o nota) lo enlaza a lo sumo un
/// borrador vivo (índices únicos parciales sobre <c>FacturaVentaNumero</c> / <c>NotaCreditoVentaNumero</c>); un borrador borrado
/// lógicamente no cuenta. Down vuelve a los índices no únicos y Up los restaura. REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class BorradoresEnlaceUnicoMigrationTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    private const string IndiceFactura = "IX_FacturasVentaBorrador_FacturaVentaNumero";
    private const string IndiceNota = "IX_NotasCreditoVentaBorrador_NotaCreditoVentaNumero";

    [Fact]
    public async Task SegundoEnlaceAlMismoDocumento_ViolaElIndiceUnico_SalvoBorradoLogico_YDownUp()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.AppConnectionString).Options;
        await using var contexto = new ApplicationDbContext(options);
        var migrador = contexto.GetService<IMigrator>();
        await migrador.MigrateAsync();
        var anterior = contexto.Database.GetMigrations().Single(m => m.EndsWith("_BorradoresConSeriesYPosteada", StringComparison.Ordinal));
        var (seriePropia, _, _) = await SeriesPrueba.CrearAsync(fixture.AppConnectionString, TipoDocumentoSerie.BorradorFacturaVenta);
        var datos = await BorradoresConSeriesYPosteadaMigrationTests.SembrarAsync(options, seriePropia);
        await using var conexion = new NpgsqlConnection(fixture.AppConnectionString);

        try
        {
            var factura = await Assert.ThrowsAsync<PostgresException>(() => conexion.ExecuteAsync(
                """UPDATE "FacturasVentaBorrador" SET "Estado" = 3, "FacturaVentaNumero" = @Numero WHERE "Id" = @Id""",
                new { Id = datos.Abierto, Numero = datos.NumeroFactura }));
            Assert.Equal(("23505", IndiceFactura), (factura.SqlState, factura.ConstraintName));
            var nota = await Assert.ThrowsAsync<PostgresException>(() => conexion.ExecuteAsync(
                """UPDATE "NotasCreditoVentaBorrador" SET "Estado" = 3, "NotaCreditoVentaNumero" = @Numero WHERE "Id" = @Id""",
                new { Id = datos.Nota, Numero = datos.NumeroNota }));
            Assert.Equal(("23505", IndiceNota), (nota.SqlState, nota.ConstraintName));

            // Un borrador borrado lógicamente no ocupa el enlace (filtro "IsDeleted" = false).
            Assert.Equal(1, await conexion.ExecuteAsync(
                """UPDATE "FacturasVentaBorrador" SET "IsDeleted" = true, "Estado" = 3, "FacturaVentaNumero" = @Numero WHERE "Id" = @Id""",
                new { Id = datos.Abierto, Numero = datos.NumeroFactura }));

            Assert.Equal((true, true), (await EsUnicoAsync(conexion, IndiceFactura), await EsUnicoAsync(conexion, IndiceNota)));
            await migrador.MigrateAsync(anterior);
            Assert.Equal((false, false), (await EsUnicoAsync(conexion, IndiceFactura), await EsUnicoAsync(conexion, IndiceNota)));
            await migrador.MigrateAsync();
            Assert.Equal((true, true), (await EsUnicoAsync(conexion, IndiceFactura), await EsUnicoAsync(conexion, IndiceNota)));
        }
        finally
        {
            await migrador.MigrateAsync();
        }
    }

    private static Task<bool> EsUnicoAsync(NpgsqlConnection conexion, string indice) => conexion.ExecuteScalarAsync<bool>(
        """
        SELECT i.indisunique FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid
        WHERE c.relname = @Indice AND c.relnamespace = 'public'::regnamespace
        """, new { Indice = indice });
}
