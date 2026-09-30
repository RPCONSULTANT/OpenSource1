using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Entities.Ventas;
using OpenSource1.Core.Enums;
using OpenSource1.Infrastructure.Data;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Infrastructure;

/// <summary>
/// Migración <c>BorradoresConSeriesYPosteada</c> (spec no-series, Parte 2) contra Postgres real. Up: los borradores existentes toman
/// FV-BORR/FV y NC-BORR/NC y las notas quedan Abierta; los CK exigen que Posteada lleve el número del documento. Down: un borrador
/// Posteada vuelve a la semántica anterior (borrado lógico con sus líneas). Vuelta a HEAD. REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class BorradoresConSeriesYPosteadaMigrationTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    [Fact]
    public async Task Up_RellenaSeriesYEstado_Down_BorraLosPosteados_YVuelta()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.AppConnectionString).Options;
        await using var contexto = new ApplicationDbContext(options);
        var migrador = contexto.GetService<IMigrator>();
        await migrador.MigrateAsync();
        var anterior = contexto.Database.GetMigrations().Single(m => m.EndsWith("_SeriesNumeracionConfigurables", StringComparison.Ordinal));
        var (seriePropia, _, _) = await SeriesPrueba.CrearAsync(fixture.AppConnectionString, TipoDocumentoSerie.BorradorFacturaVenta);
        var datos = await SembrarAsync(options, seriePropia);
        await using var conexion = new NpgsqlConnection(fixture.AppConnectionString);

        try
        {
            await migrador.MigrateAsync(anterior);

            Assert.Equal((true, (short)1), await conexion.QuerySingleAsync<(bool, short)>(
                """SELECT "IsDeleted", "Estado" FROM "FacturasVentaBorrador" WHERE "Id" = @Id""", new { Id = datos.Posteado }));
            Assert.True(await conexion.ExecuteScalarAsync<bool>(
                """SELECT "IsDeleted" FROM "LineasFacturaVentaBorrador" WHERE "FacturaVentaBorradorId" = @Id""", new { Id = datos.Posteado }));
            Assert.False(await conexion.ExecuteScalarAsync<bool>(
                """SELECT "IsDeleted" FROM "FacturasVentaBorrador" WHERE "Id" = @Id""", new { Id = datos.Abierto }));

            await migrador.MigrateAsync();

            Assert.Equal((SerieFacturaVentaIds.SerieBorradorId, SerieFacturaVentaIds.SeriePosteadaId), await conexion.QuerySingleAsync<(Guid, Guid)>(
                """SELECT "SerieBorradorId", "SerieRegistroId" FROM "FacturasVentaBorrador" WHERE "Id" = @Id""", new { Id = datos.Abierto }));
            Assert.Equal(((short)1, SerieNotaCreditoVentaIds.SerieBorradorId, SerieNotaCreditoVentaIds.SeriePosteadaId),
                await conexion.QuerySingleAsync<(short, Guid, Guid)>(
                    """SELECT "Estado", "SerieBorradorId", "SerieRegistroId" FROM "NotasCreditoVentaBorrador" WHERE "Id" = @Id""", new { Id = datos.Nota }));

            // Posteada exige el número del documento, y un número exige Posteada.
            var sinNumero = await Assert.ThrowsAsync<PostgresException>(() => conexion.ExecuteAsync(
                """UPDATE "FacturasVentaBorrador" SET "Estado" = 3 WHERE "Id" = @Id""", new { Id = datos.Abierto }));
            Assert.Equal("23514", sinNumero.SqlState);
            var notaSinNumero = await Assert.ThrowsAsync<PostgresException>(() => conexion.ExecuteAsync(
                """UPDATE "NotasCreditoVentaBorrador" SET "Estado" = 3 WHERE "Id" = @Id""", new { Id = datos.Nota }));
            Assert.Equal("23514", notaSinNumero.SqlState);
        }
        finally
        {
            await migrador.MigrateAsync();
        }
    }

    private sealed record Datos(Guid Abierto, Guid Posteado, Guid Nota);

    /// <summary>En HEAD: socio, una factura posteada (fila legal insertada a mano), un borrador abierto con una serie propia, uno Posteada con una línea de comentario y una nota abierta.</summary>
    private static async Task<Datos> SembrarAsync(DbContextOptions<ApplicationDbContext> options, Guid seriePropia)
    {
        await using var contexto = new ApplicationDbContext(options);
        var socio = new SocioNegocio
        {
            Codigo = $"MB{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            NombreComercial = "Cliente migración borradores",
            GrupoNegocioId = GrupoContableIds.NegocioNacional,
            GrupoIvaNegocioId = GrupoContableIds.IvaNegocioItbis18,
            GrupoClienteContableId = GrupoContableIds.ClienteContableGeneral,
            CreatedBy = "test",
        };
        contexto.Add(socio);
        var numeroFactura = $"MIG{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var hoy = new DateOnly(2026, 9, 29);
        contexto.FacturasVenta.Add(new FacturaVenta
        {
            Numero = numeroFactura, NumeroBorrador = numeroFactura, SocioNegocioId = socio.Id, SocioNegocioFacturarAId = socio.Id,
            NombreFacturacion = socio.NombreComercial, FechaRegistro = hoy, FechaDocumento = hoy, FechaVencimiento = hoy,
            GrupoNegocioId = socio.GrupoNegocioId!.Value, GrupoIvaNegocioId = socio.GrupoIvaNegocioId!.Value,
            GrupoClienteContableId = socio.GrupoClienteContableId!.Value, AlmacenId = AlmacenIds.Principal, Moneda = "DOP",
            CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "test",
        });

        FacturaVentaBorrador Borrador(EstadoFacturaBorrador estado, string? factura) => new()
        {
            Numero = $"MB{Guid.NewGuid():N}"[..20].ToUpperInvariant(), SocioNegocioId = socio.Id, SocioNegocioFacturarAId = socio.Id,
            NombreFacturacion = socio.NombreComercial, FechaRegistro = hoy, FechaDocumento = hoy, FechaVencimiento = hoy,
            GrupoNegocioId = socio.GrupoNegocioId!.Value, GrupoIvaNegocioId = socio.GrupoIvaNegocioId!.Value,
            GrupoClienteContableId = socio.GrupoClienteContableId!.Value, AlmacenId = AlmacenIds.Principal,
            SerieBorradorId = seriePropia, SerieRegistroId = SerieFacturaVentaIds.SeriePosteadaId,
            Estado = estado, FacturaVentaNumero = factura, CreatedBy = "test",
        };

        var abierto = Borrador(EstadoFacturaBorrador.Abierta, null);
        var posteado = Borrador(EstadoFacturaBorrador.Posteada, numeroFactura);
        contexto.FacturasVentaBorrador.AddRange(abierto, posteado);
        contexto.LineasFacturaVentaBorrador.Add(new LineaFacturaVentaBorrador
        {
            FacturaVentaBorradorId = posteado.Id, NumeroLinea = 10000, Tipo = TipoLineaFactura.Comentario, Descripcion = "Comentario", CreatedBy = "test",
        });
        var nota = new NotaCreditoVentaBorrador
        {
            Numero = $"MN{Guid.NewGuid():N}"[..20].ToUpperInvariant(), FacturaVentaNumero = numeroFactura, SocioNegocioId = socio.Id,
            SocioNegocioFacturarAId = socio.Id, NombreFacturacion = socio.NombreComercial, FechaRegistro = hoy, FechaDocumento = hoy,
            GrupoNegocioId = socio.GrupoNegocioId!.Value, GrupoIvaNegocioId = socio.GrupoIvaNegocioId!.Value,
            GrupoClienteContableId = socio.GrupoClienteContableId!.Value,
            SerieBorradorId = SerieNotaCreditoVentaIds.SerieBorradorId, SerieRegistroId = SerieNotaCreditoVentaIds.SeriePosteadaId, CreatedBy = "test",
        };
        contexto.NotasCreditoVentaBorrador.Add(nota);
        await contexto.SaveChangesAsync();
        return new Datos(abierto.Id, posteado.Id, nota.Id);
    }
}
