using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Inventario;
using OpenSource1.Core.Enums;
using OpenSource1.Infrastructure.Data;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Infrastructure;

/// <summary>
/// Verifica contra Postgres real (Task 3.3) que el libro de inventario (<c>MovimientosProducto</c>,
/// <c>MovimientosValor</c>, <c>AplicacionesMovimientoProducto</c>) es append-only: los triggers
/// <c>libro_inventario_append_only</c> bloquean UPDATE/DELETE/TRUNCATE (salvo el UPDATE puntual de
/// <c>CantidadRestante</c> en <c>MovimientosProducto</c>, que usa la aplicación FIFO de la Task 3.4) y
/// los CHECK de dominio rechazan cantidades inválidas. REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class LibroInventarioAppendOnlyTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    // Semillas de la Task 2.x/3.2: unidad "UND" y categoría "GENERAL" (para poder crear un Producto de
    // prueba) y el almacén "PRINCIPAL" (para no depender de crear uno nuevo).
    private static readonly Guid UnidadUndSemilla = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid CategoriaGeneralSemilla = Guid.Parse("c1000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task InsertarMovimientoProducto_MovimientoValor_YAplicacion_Funciona()
    {
        var (contexto, productoId, almacenId) = await PrepararAsync();
        await using var _ = contexto;

        var entrada = NuevaEntrada(productoId, almacenId, cantidad: 10m, cantidadRestante: 10m);
        contexto.Set<MovimientoProducto>().Add(entrada);
        await contexto.SaveChangesAsync();
        Assert.True(entrada.Id > 0);

        var valor = NuevoValor(entrada, productoId, almacenId);
        contexto.Set<MovimientoValor>().Add(valor);
        await contexto.SaveChangesAsync();
        Assert.True(valor.Id > 0);

        var salida = NuevaSalida(productoId, almacenId, entrada, cantidad: -4m);
        contexto.Set<MovimientoProducto>().Add(salida);
        await contexto.SaveChangesAsync();
        Assert.True(salida.Id > 0);

        var aplicacion = new AplicacionMovimientoProducto
        {
            MovimientoEntradaId = entrada.Id,
            MovimientoSalidaId = salida.Id,
            Cantidad = 4m,
            FechaRegistro = entrada.FechaRegistro
        };
        contexto.Set<AplicacionMovimientoProducto>().Add(aplicacion);
        await contexto.SaveChangesAsync();
        Assert.True(aplicacion.Id > 0);
    }

    [Fact]
    public async Task ActualizarCantidadRestante_Funciona()
    {
        var (contexto, productoId, almacenId) = await PrepararAsync();
        await using var _ = contexto;

        var entrada = NuevaEntrada(productoId, almacenId, cantidad: 10m, cantidadRestante: 10m);
        contexto.Set<MovimientoProducto>().Add(entrada);
        await contexto.SaveChangesAsync();

        await contexto.Database.ExecuteSqlAsync(
            $"UPDATE \"MovimientosProducto\" SET \"CantidadRestante\" = 0 WHERE \"Id\" = {entrada.Id}");

        var restante = await ObtenerCantidadRestanteAsync(entrada.Id);
        Assert.Equal(0m, restante);
    }

    [Fact]
    public async Task ActualizarCantidad_LanzaPostgresExceptionP0001()
    {
        var (contexto, productoId, almacenId) = await PrepararAsync();
        await using var _ = contexto;

        var entrada = NuevaEntrada(productoId, almacenId, cantidad: 10m, cantidadRestante: 10m);
        contexto.Set<MovimientoProducto>().Add(entrada);
        await contexto.SaveChangesAsync();

        var excepcion = await Assert.ThrowsAsync<PostgresException>(() =>
            contexto.Database.ExecuteSqlAsync($"UPDATE \"MovimientosProducto\" SET \"Cantidad\" = 1 WHERE \"Id\" = {entrada.Id}"));

        Assert.Equal("P0001", excepcion.SqlState);
    }

    [Fact]
    public async Task ActualizarImporteCostoEnMovimientosValor_LanzaPostgresExceptionP0001()
    {
        var (contexto, productoId, almacenId) = await PrepararAsync();
        await using var _ = contexto;

        var entrada = NuevaEntrada(productoId, almacenId, cantidad: 10m, cantidadRestante: 10m);
        contexto.Set<MovimientoProducto>().Add(entrada);
        await contexto.SaveChangesAsync();

        var valor = NuevoValor(entrada, productoId, almacenId);
        contexto.Set<MovimientoValor>().Add(valor);
        await contexto.SaveChangesAsync();

        var excepcion = await Assert.ThrowsAsync<PostgresException>(() =>
            contexto.Database.ExecuteSqlAsync($"UPDATE \"MovimientosValor\" SET \"ImporteCosto\" = 0 WHERE \"Id\" = {valor.Id}"));

        Assert.Equal("P0001", excepcion.SqlState);
    }

    [Fact]
    public async Task Delete_EnMovimientosProducto_LanzaPostgresExceptionP0001()
    {
        var (contexto, productoId, almacenId) = await PrepararAsync();
        await using var _ = contexto;

        var entrada = NuevaEntrada(productoId, almacenId, cantidad: 10m, cantidadRestante: 10m);
        contexto.Set<MovimientoProducto>().Add(entrada);
        await contexto.SaveChangesAsync();

        var excepcion = await Assert.ThrowsAsync<PostgresException>(() =>
            contexto.Database.ExecuteSqlAsync($"DELETE FROM \"MovimientosProducto\" WHERE \"Id\" = {entrada.Id}"));

        Assert.Equal("P0001", excepcion.SqlState);
    }

    [Fact]
    public async Task Delete_EnMovimientosValor_LanzaPostgresExceptionP0001()
    {
        var (contexto, productoId, almacenId) = await PrepararAsync();
        await using var _ = contexto;

        var entrada = NuevaEntrada(productoId, almacenId, cantidad: 10m, cantidadRestante: 10m);
        contexto.Set<MovimientoProducto>().Add(entrada);
        await contexto.SaveChangesAsync();

        var valor = NuevoValor(entrada, productoId, almacenId);
        contexto.Set<MovimientoValor>().Add(valor);
        await contexto.SaveChangesAsync();

        var excepcion = await Assert.ThrowsAsync<PostgresException>(() =>
            contexto.Database.ExecuteSqlAsync($"DELETE FROM \"MovimientosValor\" WHERE \"Id\" = {valor.Id}"));

        Assert.Equal("P0001", excepcion.SqlState);
    }

    [Fact]
    public async Task Delete_EnAplicacionesMovimientoProducto_LanzaPostgresExceptionP0001()
    {
        var (contexto, productoId, almacenId) = await PrepararAsync();
        await using var _ = contexto;

        var entrada = NuevaEntrada(productoId, almacenId, cantidad: 10m, cantidadRestante: 10m);
        contexto.Set<MovimientoProducto>().Add(entrada);
        await contexto.SaveChangesAsync();

        var salida = NuevaSalida(productoId, almacenId, entrada, cantidad: -4m);
        contexto.Set<MovimientoProducto>().Add(salida);
        await contexto.SaveChangesAsync();

        var aplicacion = new AplicacionMovimientoProducto
        {
            MovimientoEntradaId = entrada.Id,
            MovimientoSalidaId = salida.Id,
            Cantidad = 4m,
            FechaRegistro = entrada.FechaRegistro
        };
        contexto.Set<AplicacionMovimientoProducto>().Add(aplicacion);
        await contexto.SaveChangesAsync();

        var excepcion = await Assert.ThrowsAsync<PostgresException>(() =>
            contexto.Database.ExecuteSqlAsync($"DELETE FROM \"AplicacionesMovimientoProducto\" WHERE \"Id\" = {aplicacion.Id}"));

        Assert.Equal("P0001", excepcion.SqlState);
    }

    // MovimientosValor y AplicacionesMovimientoProducto no tienen dependientes propios: TRUNCATE llega
    // directo al trigger de sentencia "TR_..._NoTruncate", que lo bloquea con P0001. MovimientosProducto SÍ
    // tiene dependientes (MovimientosValor.MovimientoProductoId y las dos FK de AplicacionesMovimientoProducto):
    // Postgres rechaza truncar una tabla referenciada por FK sin incluir también a sus dependientes ANTES de
    // llegar a disparar ningún trigger, con "0A000" (feature_not_supported) — una protección más fuerte que la
    // nuestra, no una laguna: para llegar a intentar de verdad un TRUNCATE de esa tabla hay que incluir a sus
    // dependientes (o CASCADE), y ahí es donde entra nuestro trigger (ver el test de abajo).
    [Theory]
    [InlineData("MovimientosProducto", "0A000")]
    [InlineData("MovimientosValor", "P0001")]
    [InlineData("AplicacionesMovimientoProducto", "P0001")]
    public async Task Truncate_EnCualquierTablaDelLibro_QuedaBloqueado(string tabla, string sqlStateEsperado)
    {
        var (contexto, _, _) = await PrepararAsync();
        await using var _2 = contexto;

        // El nombre de tabla es un identificador (no un valor): no se puede parametrizar con
        // ExecuteSqlAsync/FormattableString. Viene solo de los literales fijos de [InlineData] de arriba,
        // nunca de entrada externa, así que ExecuteSqlRaw es seguro aquí.
#pragma warning disable EF1002
        var excepcion = await Assert.ThrowsAsync<PostgresException>(() =>
            contexto.Database.ExecuteSqlRawAsync($"TRUNCATE \"{tabla}\""));
#pragma warning restore EF1002

        Assert.Equal(sqlStateEsperado, excepcion.SqlState);
    }

    [Fact]
    public async Task Truncate_DeLasTresTablasJuntasConCascade_LanzaPostgresExceptionP0001()
    {
        // Con las tres tablas en la misma sentencia (o CASCADE) Postgres ya no rechaza el TRUNCATE por FK
        // pendientes: llega a disparar los triggers de sentencia, que lo bloquean igual que a las otras dos.
        var (contexto, _, _) = await PrepararAsync();
        await using var _2 = contexto;

        var excepcion = await Assert.ThrowsAsync<PostgresException>(() =>
            contexto.Database.ExecuteSqlRawAsync(
                "TRUNCATE \"MovimientosProducto\", \"MovimientosValor\", \"AplicacionesMovimientoProducto\" CASCADE"));

        Assert.Equal("P0001", excepcion.SqlState);
    }

    [Fact]
    public async Task Insertar_ConCantidadCero_ViolaCheckDeDominio()
    {
        var (contexto, productoId, almacenId) = await PrepararAsync();
        await using var _ = contexto;

        var entrada = NuevaEntrada(productoId, almacenId, cantidad: 0m, cantidadRestante: null);
        contexto.Set<MovimientoProducto>().Add(entrada);

        var excepcion = await Assert.ThrowsAsync<DbUpdateException>(() => contexto.SaveChangesAsync());

        Assert.Equal("23514", Assert.IsType<PostgresException>(excepcion.InnerException).SqlState);
    }

    [Fact]
    public async Task Insertar_ConCantidadRestanteMayorQueCantidad_ViolaCheckDeDominio()
    {
        var (contexto, productoId, almacenId) = await PrepararAsync();
        await using var _ = contexto;

        var entrada = NuevaEntrada(productoId, almacenId, cantidad: 5m, cantidadRestante: 6m);
        contexto.Set<MovimientoProducto>().Add(entrada);

        var excepcion = await Assert.ThrowsAsync<DbUpdateException>(() => contexto.SaveChangesAsync());

        Assert.Equal("23514", Assert.IsType<PostgresException>(excepcion.InnerException).SqlState);
    }

    /// <summary>
    /// Task 5.6 (desviación de la Fase 5): la única columna actualizable de <c>MovimientosValor</c> es
    /// <c>ImporteCostoPosteadoContabilidad</c> (la iguala a <c>ImporteCosto</c> el batch de costo), como <c>CantidadRestante</c> en
    /// <c>MovimientosProducto</c>.
    /// </summary>
    [Fact]
    public async Task ActualizarSoloImporteCostoPosteadoContabilidad_EnMovimientosValor_Funciona()
    {
        var valor = await InsertarValorAsync();
        await using var contexto = NuevoContextoPropio();

        await contexto.Database.ExecuteSqlAsync(
            $"UPDATE \"MovimientosValor\" SET \"ImporteCostoPosteadoContabilidad\" = \"ImporteCosto\" WHERE \"Id\" = {valor.Id}");

        var posteado = await contexto.Set<MovimientoValor>().Where(x => x.Id == valor.Id)
            .Select(x => x.ImporteCostoPosteadoContabilidad).SingleAsync();
        Assert.Equal(valor.ImporteCosto, posteado);
    }

    /// <summary>Cambiar la columna permitida JUNTO con cualquier otra sigue prohibido (el trigger compara el resto de la fila).</summary>
    [Theory]
    [InlineData("\"ImporteCosto\" = 0")]
    [InlineData("\"CantidadValorada\" = 0")]
    [InlineData("\"FechaRegistro\" = \"FechaRegistro\" + 1")]
    [InlineData("\"ClaveOrigen\" = 'X'")]
    public async Task ActualizarPosteadoJuntoConOtraColumna_EnMovimientosValor_LanzaPostgresExceptionP0001(string otraColumna)
    {
        var valor = await InsertarValorAsync();
        await using var contexto = NuevoContextoPropio();

        // La asignación extra es SQL (no un valor) y sale solo de los literales fijos de [InlineData]: ExecuteSqlRaw es seguro.
#pragma warning disable EF1002
        var excepcion = await Assert.ThrowsAsync<PostgresException>(() => contexto.Database.ExecuteSqlRawAsync(
            $"UPDATE \"MovimientosValor\" SET \"ImporteCostoPosteadoContabilidad\" = 1, {otraColumna} WHERE \"Id\" = {valor.Id}"));
#pragma warning restore EF1002

        Assert.Equal("P0001", excepcion.SqlState);
    }

    /// <summary>La excepción es SOLO para MovimientosValor: el resto de tablas del libro que comparten la función siguen sin UPDATE.</summary>
    [Fact]
    public async Task ActualizarSinCambios_EnRegistrosContables_SigueLanzandoP0001()
    {
        await using var contexto = NuevoContextoPropio();
        // Migra como sus hermanos (PrepararAsync/InsertarValorAsync): si esta prueba corre la primera en un contenedor nuevo,
        // la tabla aún no existe (42P01) y el resultado dependería del orden.
        await contexto.GetService<IMigrator>().MigrateAsync();
        var excepcion = await Assert.ThrowsAsync<PostgresException>(() => contexto.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"RegistrosContables\" (\"NumeroRegistro\", \"DesdeMovimiento\", \"HastaMovimiento\", \"FechaCreacion\", \"CreadoPor\", \"TipoOrigen\", \"ClaveOrigen\") " +
            "VALUES ('T-UPD', 1, 1, now(), 'test', 4, 'X'); UPDATE \"RegistrosContables\" SET \"ClaveOrigen\" = \"ClaveOrigen\" WHERE \"NumeroRegistro\" = 'T-UPD'"));

        Assert.Equal("P0001", excepcion.SqlState);
    }

    private async Task<MovimientoValor> InsertarValorAsync()
    {
        var (contexto, productoId, almacenId) = await PrepararAsync();
        await using var _ = contexto;
        var entrada = NuevaEntrada(productoId, almacenId, cantidad: 10m, cantidadRestante: 10m);
        contexto.Set<MovimientoProducto>().Add(entrada);
        await contexto.SaveChangesAsync();
        var valor = NuevoValor(entrada, productoId, almacenId);
        contexto.Set<MovimientoValor>().Add(valor);
        await contexto.SaveChangesAsync();
        return valor;
    }

    private ApplicationDbContext NuevoContextoPropio() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.AppConnectionString).Options);

    private static MovimientoProducto NuevaEntrada(Guid productoId, Guid almacenId, decimal cantidad, decimal? cantidadRestante)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        return new MovimientoProducto
        {
            ProductoId = productoId,
            AlmacenId = almacenId,
            TipoMovimiento = TipoMovimientoInventario.Compra,
            TipoDocumento = TipoDocumentoInventario.RegistroDiario,
            NumeroLineaDocumento = 1,
            FechaRegistro = hoy,
            FechaDocumento = hoy,
            Cantidad = cantidad,
            CantidadRestante = cantidadRestante,
            CantidadFacturada = cantidad,
            UnidadMedidaId = UnidadUndSemilla,
            CantidadPorUnidadMedida = 1m,
            TipoOrigen = TipoOrigenMovimiento.Diario,
            ClaveOrigen = $"TEST-ENTRADA-{Guid.NewGuid():N}",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "test"
        };
    }

    private static MovimientoProducto NuevaSalida(Guid productoId, Guid almacenId, MovimientoProducto entrada, decimal cantidad) => new()
    {
        ProductoId = productoId,
        AlmacenId = almacenId,
        TipoMovimiento = TipoMovimientoInventario.Venta,
        TipoDocumento = TipoDocumentoInventario.FacturaVenta,
        NumeroLineaDocumento = 1,
        FechaRegistro = entrada.FechaRegistro,
        FechaDocumento = entrada.FechaDocumento,
        Cantidad = cantidad,
        CantidadRestante = null,
        CantidadFacturada = -cantidad,
        UnidadMedidaId = UnidadUndSemilla,
        CantidadPorUnidadMedida = 1m,
        TipoOrigen = TipoOrigenMovimiento.FacturaVenta,
        ClaveOrigen = $"TEST-SALIDA-{Guid.NewGuid():N}",
        CreatedAtUtc = DateTimeOffset.UtcNow,
        CreatedBy = "test"
    };

    private static MovimientoValor NuevoValor(MovimientoProducto entrada, Guid productoId, Guid almacenId) => new()
    {
        MovimientoProductoId = entrada.Id,
        ProductoId = productoId,
        AlmacenId = almacenId,
        TipoValor = TipoValor.CostoDirecto,
        TipoMovimiento = TipoMovimientoInventario.Compra,
        FechaRegistro = entrada.FechaRegistro,
        CantidadValorada = entrada.Cantidad,
        CantidadFacturada = entrada.CantidadFacturada,
        ImporteCosto = entrada.Cantidad * 10m,
        CostoPorUnidad = 10m,
        ImporteVenta = 0m,
        ImporteCostoPosteadoContabilidad = 0m,
        TipoDocumento = entrada.TipoDocumento,
        NumeroLineaDocumento = entrada.NumeroLineaDocumento,
        TipoOrigen = entrada.TipoOrigen,
        ClaveOrigen = entrada.ClaveOrigen,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        CreatedBy = "test"
    };

    private async Task<decimal?> ObtenerCantidadRestanteAsync(long movimientoId)
    {
        await using var conexion = new NpgsqlConnection(fixture.AppConnectionString);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand(
            $"SELECT \"CantidadRestante\" FROM \"MovimientosProducto\" WHERE \"Id\" = {movimientoId}", conexion);
        var resultado = await comando.ExecuteScalarAsync();
        return resultado is null or DBNull ? null : (decimal)resultado;
    }

    /// <summary>
    /// Contexto propio por prueba (conexión propia) y un producto nuevo (unidad/categoría semilla, almacén
    /// PRINCIPAL) contra el que registrar movimientos, sin depender de datos dejados por otras pruebas de
    /// la clase (todas comparten el mismo contenedor Postgres, ver <see cref="PostgresTestFixture"/>).
    /// </summary>
    private async Task<(ApplicationDbContext Contexto, Guid ProductoId, Guid AlmacenId)> PrepararAsync()
    {
        var contexto = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.AppConnectionString).Options);
        var migrador = contexto.GetService<IMigrator>();
        await migrador.MigrateAsync();

        var producto = new Producto
        {
            Codigo = $"LIB{Guid.NewGuid():N}"[..20],
            Nombre = "Producto de prueba del libro de inventario",
            PrecioVenta = 10m,
            UnidadMedidaBaseId = UnidadUndSemilla,
            CategoriaId = CategoriaGeneralSemilla,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "test"
        };
        contexto.Productos.Add(producto);
        await contexto.SaveChangesAsync();

        return (contexto, producto.Id, AlmacenIds.Principal);
    }
}
