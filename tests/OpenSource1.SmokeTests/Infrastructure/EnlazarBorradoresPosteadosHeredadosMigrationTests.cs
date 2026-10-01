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
/// Migración <c>EnlazarBorradoresPosteadosHeredados</c> contra Postgres real. Antes de la rama no-series el posteo borraba lógicamente el
/// borrador y todas sus líneas vivas; Up los recupera como Posteada enlazados a su documento (solo las líneas que borró el posteo), sin
/// tocar borradores ni líneas que borró el usuario ni documentos que ya enlaza un borrador vivo. Down deshace exactamente esas filas y
/// la vuelta a Up las enlaza otra vez. REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class EnlazarBorradoresPosteadosHeredadosMigrationTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    [Fact]
    public async Task Up_EnlazaLosPosteadosHeredadosConSusLineas_Down_LosDevuelveBorrados_YVuelta()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.AppConnectionString).Options;
        await using var contexto = new ApplicationDbContext(options);
        var migrador = contexto.GetService<IMigrator>();
        await migrador.MigrateAsync();
        var anterior = contexto.Database.GetMigrations().Single(m => m.EndsWith("_BorradoresEnlaceUnico", StringComparison.Ordinal));
        await using var conexion = new NpgsqlConnection(fixture.AppConnectionString);

        try
        {
            await migrador.MigrateAsync(anterior);
            var d = await SembrarHeredadosAsync(options);

            await migrador.MigrateAsync();
            await AfirmarEnlazadosAsync(options, conexion, d);

            await migrador.MigrateAsync(anterior);

            // Down: el borrador vuelve a estar borrado con su estado anterior (Liberada), sin enlace y con la hora del posteo.
            Assert.Equal((true, (short)EstadoFacturaBorrador.Liberada, (string?)null, d.BorradoEn, "migracion"),
                await conexion.QuerySingleAsync<(bool, short, string?, DateTimeOffset?, string?)>(
                    """SELECT "IsDeleted", "Estado", "FacturaVentaNumero", "DeletedAtUtc", "DeletedBy" FROM "FacturasVentaBorrador" WHERE "Id" = @Id""",
                    new { Id = d.Factura }));
            Assert.Equal((true, (short)EstadoNotaCreditoBorrador.Abierta, (string?)null, d.BorradoEn),
                await conexion.QuerySingleAsync<(bool, short, string?, DateTimeOffset?)>(
                    """SELECT "IsDeleted", "Estado", "NotaCreditoVentaNumero", "DeletedAtUtc" FROM "NotasCreditoVentaBorrador" WHERE "Id" = @Id""",
                    new { Id = d.Nota }));
            Assert.Equal(0, await conexion.ExecuteScalarAsync<int>(
                """SELECT COUNT(*) FROM "LineasFacturaVentaBorrador" WHERE "FacturaVentaBorradorId" = @Id AND "IsDeleted" = false""",
                new { Id = d.Factura }));
            Assert.Equal((d.LineaBorradaEn, "migracion"), await conexion.QuerySingleAsync<(DateTimeOffset, string)>(
                """SELECT "DeletedAtUtc", "DeletedBy" FROM "LineasFacturaVentaBorrador" WHERE "Id" = @Id""", new { Id = d.LineaPosteada }));
            Assert.Equal(0, await conexion.ExecuteScalarAsync<int>(
                """SELECT COUNT(*) FROM "LineasNotaCreditoVentaBorrador" WHERE "NotaCreditoVentaBorradorId" = @Id AND "IsDeleted" = false""",
                new { Id = d.Nota }));
            // La línea que el usuario borró antes del posteo no se toca en ningún sentido.
            Assert.Equal((d.LineaManualBorradaEn, "usuario"), await conexion.QuerySingleAsync<(DateTimeOffset, string)>(
                """SELECT "DeletedAtUtc", "DeletedBy" FROM "LineasFacturaVentaBorrador" WHERE "Id" = @Id""", new { Id = d.LineaManual }));

            await migrador.MigrateAsync();
            await AfirmarEnlazadosAsync(options, conexion, d);
        }
        finally
        {
            await migrador.MigrateAsync();
        }
    }

    private static async Task AfirmarEnlazadosAsync(DbContextOptions<ApplicationDbContext> options, NpgsqlConnection conexion, Heredados d)
    {
        // El borrador posteado heredado es visible (filtro de borrado lógico) como Posteada, enlazado a su factura y con sus dos líneas.
        await using (var contexto = new ApplicationDbContext(options))
        {
            var factura = await contexto.FacturasVentaBorrador.SingleAsync(x => x.Id == d.Factura);
            Assert.Equal((EstadoFacturaBorrador.Posteada, d.NumeroFactura, (DateTimeOffset?)null, (string?)null),
                (factura.Estado, factura.FacturaVentaNumero, factura.DeletedAtUtc, factura.DeletedBy));
            Assert.Equal([d.LineaPosteada, d.LineaComentario], await contexto.LineasFacturaVentaBorrador
                .Where(x => x.FacturaVentaBorradorId == d.Factura).OrderBy(x => x.NumeroLinea).Select(x => x.Id).ToListAsync());

            var nota = await contexto.NotasCreditoVentaBorrador.SingleAsync(x => x.Id == d.Nota);
            Assert.Equal((EstadoNotaCreditoBorrador.Posteada, d.NumeroNota), (nota.Estado, nota.NotaCreditoVentaNumero));
            Assert.Equal([d.LineaNota], await contexto.LineasNotaCreditoVentaBorrador
                .Where(x => x.NotaCreditoVentaBorradorId == d.Nota).Select(x => x.Id).ToListAsync());

            // El borrador que borró el usuario (sin documento) sigue borrado, y la línea borrada a mano antes del posteo también.
            Assert.False(await contexto.FacturasVentaBorrador.AnyAsync(x => x.Id == d.BorradoPorUsuario));
            Assert.False(await contexto.LineasFacturaVentaBorrador.AnyAsync(x => x.Id == d.LineaManual));
        }

        // La factura y la nota muestran su borrador (JOIN de los repositorios de lectura).
        Assert.Equal(d.Factura, await conexion.ExecuteScalarAsync<Guid?>(
            """SELECT fb."Id" FROM "FacturasVenta" f JOIN "FacturasVentaBorrador" fb ON fb."FacturaVentaNumero" = f."Numero" AND fb."IsDeleted" = false WHERE f."Numero" = @N""",
            new { N = d.NumeroFactura }));
        Assert.Equal(d.Nota, await conexion.ExecuteScalarAsync<Guid?>(
            """SELECT nb."Id" FROM "NotasCreditoVenta" n JOIN "NotasCreditoVentaBorrador" nb ON nb."NotaCreditoVentaNumero" = n."Numero" AND nb."IsDeleted" = false WHERE n."Numero" = @N""",
            new { N = d.NumeroNota }));

        // Documento ya enlazado por un borrador vivo: el duplicado borrado sigue borrado y sin enlace (índices únicos intactos).
        Assert.Equal((true, (string?)null), await conexion.QuerySingleAsync<(bool, string?)>(
            """SELECT "IsDeleted", "FacturaVentaNumero" FROM "FacturasVentaBorrador" WHERE "Id" = @Id""", new { Id = d.DuplicadoBorrado }));
        Assert.Equal((false, d.NumeroFacturaEnlazada), await conexion.QuerySingleAsync<(bool, string?)>(
            """SELECT "IsDeleted", "FacturaVentaNumero" FROM "FacturasVentaBorrador" WHERE "Id" = @Id""", new { Id = d.EnlazadoVivo }));
    }

    private sealed record Heredados(
        Guid Factura, Guid LineaPosteada, Guid LineaComentario, Guid LineaManual, Guid Nota, Guid LineaNota, Guid BorradoPorUsuario,
        Guid DuplicadoBorrado, Guid EnlazadoVivo, string NumeroFactura, string NumeroNota, string NumeroFacturaEnlazada,
        DateTimeOffset BorradoEn, DateTimeOffset LineaBorradaEn, DateTimeOffset LineaManualBorradaEn);

    /// <summary>
    /// Filas con la forma que dejaba el posteo anterior a la rama: documento con <c>NumeroBorrador</c> = número del borrador y
    /// <c>CreatedAtUtc</c> = T; líneas vivas borradas en T + 1 ms por el creador del documento; borrador borrado al confirmar (T + 2 ms),
    /// sin enlace. Además un borrador que borró el usuario, una línea borrada a mano una hora antes del posteo y una factura que ya enlaza
    /// un borrador vivo junto a un borrador borrado con el mismo número.
    /// </summary>
    private static async Task<Heredados> SembrarHeredadosAsync(DbContextOptions<ApplicationDbContext> options)
    {
        await using var contexto = new ApplicationDbContext(options);
        var ahora = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var lineaBorradaEn = ahora.AddMilliseconds(1);
        var borradoEn = ahora.AddMilliseconds(2);
        var manualEn = ahora.AddHours(-1);
        var hoy = new DateOnly(2026, 9, 29);
        var socio = new SocioNegocio
        {
            Codigo = $"EH{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            NombreComercial = "Cliente borradores heredados",
            GrupoNegocioId = GrupoContableIds.NegocioNacional,
            GrupoIvaNegocioId = GrupoContableIds.IvaNegocioItbis18,
            GrupoClienteContableId = GrupoContableIds.ClienteContableGeneral,
            CreatedBy = "test",
        };
        contexto.Add(socio);

        string Numero(string prefijo) => $"{prefijo}{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        FacturaVenta Factura(string numero, string numeroBorrador) => new()
        {
            Numero = numero, NumeroBorrador = numeroBorrador, SocioNegocioId = socio.Id, SocioNegocioFacturarAId = socio.Id,
            NombreFacturacion = socio.NombreComercial, FechaRegistro = hoy, FechaDocumento = hoy, FechaVencimiento = hoy,
            GrupoNegocioId = socio.GrupoNegocioId!.Value, GrupoIvaNegocioId = socio.GrupoIvaNegocioId!.Value,
            GrupoClienteContableId = socio.GrupoClienteContableId!.Value, AlmacenId = AlmacenIds.Principal, Moneda = "DOP",
            CreatedAtUtc = ahora, CreatedBy = "Usuario Posteo",
        };
        FacturaVentaBorrador Borrador(string numero, EstadoFacturaBorrador estado, string? enlace, DateTimeOffset? borradoEnUtc) => new()
        {
            Numero = numero, SocioNegocioId = socio.Id, SocioNegocioFacturarAId = socio.Id,
            NombreFacturacion = socio.NombreComercial, FechaRegistro = hoy, FechaDocumento = hoy, FechaVencimiento = hoy,
            GrupoNegocioId = socio.GrupoNegocioId!.Value, GrupoIvaNegocioId = socio.GrupoIvaNegocioId!.Value,
            GrupoClienteContableId = socio.GrupoClienteContableId!.Value, AlmacenId = AlmacenIds.Principal,
            SerieBorradorId = SerieFacturaVentaIds.SerieBorradorId, SerieRegistroId = SerieFacturaVentaIds.SeriePosteadaId,
            Estado = estado, FacturaVentaNumero = enlace, CreatedBy = "test",
            IsDeleted = borradoEnUtc is not null, DeletedAtUtc = borradoEnUtc, DeletedBy = borradoEnUtc is null ? null : "usuario",
            UpdatedAtUtc = borradoEnUtc, UpdatedBy = borradoEnUtc is null ? null : "usuario",
        };
        LineaFacturaVentaBorrador Linea(Guid borradorId, int numero, TipoLineaFactura tipo, DateTimeOffset borradaEn, string por) => new()
        {
            FacturaVentaBorradorId = borradorId, NumeroLinea = numero, Tipo = tipo, Descripcion = "Línea",
            CuentaContableId = tipo == TipoLineaFactura.Comentario ? null : CuentaContableIds.Caja,
            CantidadPorUnidadMedida = tipo == TipoLineaFactura.Comentario ? 0 : 1, Cantidad = tipo == TipoLineaFactura.Comentario ? 0 : 1,
            PrecioUnitario = tipo == TipoLineaFactura.Comentario ? 0 : 100, ImporteLinea = tipo == TipoLineaFactura.Comentario ? 0 : 100,
            CreatedBy = "test", IsDeleted = true, DeletedAtUtc = borradaEn, DeletedBy = por,
        };

        // Factura posteada heredada (el borrador estaba Liberada al postear).
        var numeroBorrador = Numero("EHB");
        var numeroFactura = Numero("EHF");
        contexto.FacturasVenta.Add(Factura(numeroFactura, numeroBorrador));
        var lineaFactura = new LineaFacturaVenta
        {
            FacturaVentaNumero = numeroFactura, NumeroLinea = 10000, Tipo = TipoLineaFactura.CuentaContable, CuentaContableId = CuentaContableIds.Caja,
            Descripcion = "Cuenta", CantidadPorUnidadMedida = 1, Cantidad = 1, PrecioUnitario = 100, ImporteLinea = 100,
        };
        contexto.LineasFacturaVenta.Add(lineaFactura);
        var factura = Borrador(numeroBorrador, EstadoFacturaBorrador.Liberada, null, borradoEn);
        var lineaPosteada = Linea(factura.Id, 10000, TipoLineaFactura.CuentaContable, lineaBorradaEn, "Usuario Posteo");
        var lineaComentario = Linea(factura.Id, 20000, TipoLineaFactura.Comentario, lineaBorradaEn, "Usuario Posteo");
        // Mismo NumeroLinea que la posteada: resucitarla rompería además el índice único parcial de las líneas.
        var lineaManual = Linea(factura.Id, 10000, TipoLineaFactura.CuentaContable, manualEn, "usuario");

        // Borrador que borró el usuario: ningún documento lo nombra.
        var borradoPorUsuario = Borrador(Numero("EHU"), EstadoFacturaBorrador.Abierta, null, manualEn);

        // Factura ya enlazada por un borrador vivo + un borrador borrado con el mismo número de borrador.
        var numeroDuplicado = Numero("EHD");
        var numeroFacturaEnlazada = Numero("EHE");
        contexto.FacturasVenta.Add(Factura(numeroFacturaEnlazada, numeroDuplicado));
        var enlazadoVivo = Borrador(numeroDuplicado, EstadoFacturaBorrador.Posteada, numeroFacturaEnlazada, null);
        var duplicadoBorrado = Borrador(numeroDuplicado, EstadoFacturaBorrador.Abierta, null, borradoEn);

        contexto.FacturasVentaBorrador.AddRange(factura, borradoPorUsuario, enlazadoVivo, duplicadoBorrado);
        contexto.LineasFacturaVentaBorrador.AddRange(lineaPosteada, lineaComentario, lineaManual);
        await contexto.SaveChangesAsync();

        // Nota de crédito posteada heredada sobre la factura.
        var numeroBorradorNota = Numero("EHN");
        var numeroNota = Numero("EHC");
        contexto.NotasCreditoVenta.Add(new NotaCreditoVenta
        {
            Numero = numeroNota, NumeroBorrador = numeroBorradorNota, FacturaVentaNumero = numeroFactura, SocioNegocioId = socio.Id,
            SocioNegocioFacturarAId = socio.Id, NombreFacturacion = socio.NombreComercial, FechaRegistro = hoy, FechaDocumento = hoy,
            GrupoNegocioId = socio.GrupoNegocioId!.Value, GrupoIvaNegocioId = socio.GrupoIvaNegocioId!.Value,
            GrupoClienteContableId = socio.GrupoClienteContableId!.Value, Moneda = "DOP", CreatedAtUtc = ahora, CreatedBy = "Usuario Posteo",
        });
        var nota = new NotaCreditoVentaBorrador
        {
            Numero = numeroBorradorNota, FacturaVentaNumero = numeroFactura, SocioNegocioId = socio.Id,
            SocioNegocioFacturarAId = socio.Id, NombreFacturacion = socio.NombreComercial, FechaRegistro = hoy, FechaDocumento = hoy,
            GrupoNegocioId = socio.GrupoNegocioId!.Value, GrupoIvaNegocioId = socio.GrupoIvaNegocioId!.Value,
            GrupoClienteContableId = socio.GrupoClienteContableId!.Value,
            SerieBorradorId = SerieNotaCreditoVentaIds.SerieBorradorId, SerieRegistroId = SerieNotaCreditoVentaIds.SeriePosteadaId,
            CreatedBy = "test", IsDeleted = true, DeletedAtUtc = borradoEn, DeletedBy = "usuario", UpdatedAtUtc = borradoEn, UpdatedBy = "usuario",
        };
        var lineaNota = new LineaNotaCreditoVentaBorrador
        {
            NotaCreditoVentaBorradorId = nota.Id, LineaFacturaVentaId = lineaFactura.Id, NumeroLinea = 10000, Tipo = TipoLineaFactura.CuentaContable,
            CuentaContableId = CuentaContableIds.Caja, Descripcion = "Cuenta", CantidadPorUnidadMedida = 1, Cantidad = 1, PrecioUnitario = 100,
            ImporteLinea = 100, CreatedBy = "test", IsDeleted = true, DeletedAtUtc = lineaBorradaEn, DeletedBy = "Usuario Posteo",
        };
        contexto.NotasCreditoVentaBorrador.Add(nota);
        contexto.LineasNotaCreditoVentaBorrador.Add(lineaNota);
        await contexto.SaveChangesAsync();

        return new Heredados(
            factura.Id, lineaPosteada.Id, lineaComentario.Id, lineaManual.Id, nota.Id, lineaNota.Id, borradoPorUsuario.Id,
            duplicadoBorrado.Id, enlazadoVivo.Id, numeroFactura, numeroNota, numeroFacturaEnlazada, borradoEn, lineaBorradaEn, manualEn);
    }
}
