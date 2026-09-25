using Dapper;
using Microsoft.Extensions.DependencyInjection;
using OpenSource1.Application.Data;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Services;

/// <summary>
/// <see cref="IConsultaInventario"/> contra Postgres real (Task 3.4): existencia derivada con cortes por almacén y fecha,
/// existencias por almacén y el costo promedio móvil por día de las desviaciones de la Fase 3. REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ConsultaInventarioTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private static readonly DateOnly D1 = new(2026, 4, 1);
    private static readonly DateOnly D2 = new(2026, 4, 2);
    private static readonly DateOnly D3 = new(2026, 4, 3);

    private readonly LibroInventarioPrueba _prueba = new(fixture);

    public Task InitializeAsync() => _prueba.InicializarAsync();

    public async Task DisposeAsync() => await _prueba.DisposeAsync();

    [Fact]
    public async Task Existencia_SinMovimientos_EsCero_YSinAlmacenesListados()
    {
        var producto = await _prueba.SembrarProductoAsync();

        Assert.Equal(0m, await _prueba.ConsultarAsync(c => c.ExistenciaAsync(producto, null, null)));
        Assert.Empty(await _prueba.ConsultarAsync(c => c.ExistenciasPorAlmacenAsync(producto)));
        Assert.Null(await _prueba.ConsultarAsync(c => c.CostoPromedioAsync(producto, D1)));
    }

    [Fact]
    public async Task Existencia_MovimientosMezclados_FiltraPorAlmacenYFecha()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var a = await _prueba.SembrarAlmacenAsync();
        var b = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, a, 10m, 1m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, b, 4m, 1m, D2));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, a, 3m, D3));

        Assert.Equal(11m, await _prueba.ConsultarAsync(c => c.ExistenciaAsync(producto, null, null)));
        Assert.Equal(7m, await _prueba.ConsultarAsync(c => c.ExistenciaAsync(producto, a, null)));
        Assert.Equal(10m, await _prueba.ConsultarAsync(c => c.ExistenciaAsync(producto, a, D2)));
        Assert.Equal(14m, await _prueba.ConsultarAsync(c => c.ExistenciaAsync(producto, null, D2)));
        Assert.Equal(10m, await _prueba.ConsultarAsync(c => c.ExistenciaAsync(producto, null, D1)));

        var porAlmacen = await _prueba.ConsultarAsync(c => c.ExistenciasPorAlmacenAsync(producto));
        Assert.Equal(2, porAlmacen.Count);
        var filaA = Assert.Single(porAlmacen, x => x.AlmacenId == a);
        Assert.Equal(7m, filaA.Existencia);
        Assert.Equal("Almacén de prueba", filaA.AlmacenNombre);
        Assert.False(string.IsNullOrWhiteSpace(filaA.AlmacenCodigo));
        Assert.Equal(porAlmacen.OrderBy(x => x.AlmacenCodigo, StringComparer.Ordinal).ToList(), porAlmacen);
    }

    [Fact]
    public async Task CostoPromedio_EntradasADistintoPrecio_DaElPonderado()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 10m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 30m, 20m, D1));

        Assert.Equal(17.5m, await _prueba.ConsultarAsync(c => c.CostoPromedioAsync(producto, D1)));
    }

    [Fact]
    public async Task CostoPromedio_MismoDia_IncluyeEntradasNoTransferencia_ExcluyeSalidasYTransferencias()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var a = await _prueba.SembrarAlmacenAsync();
        var b = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, a, 10m, 10m, D1));
        var salida = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, a, 5m, D2));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, a, 10m, 20m, D2));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(
            producto, b, 10m, 99m, D2, tipo: OpenSource1.Core.Enums.TipoMovimientoInventario.Transferencia));

        // La salida de D2 se registró antes que la entrada del mismo día: usó el promedio de D1.
        Assert.Equal(-50m, salida.ImporteCosto);

        // Para D2: V = 100 (D1) + 200 (entrada no transferencia de D2); la salida y la transferencia de D2 no cuentan.
        Assert.Equal(15m, await _prueba.ConsultarAsync(c => c.CostoPromedioAsync(producto, D2)));

        // Para D3 cuenta todo lo anterior: (100 - 50 + 200 + 990) / (10 - 5 + 10 + 10).
        Assert.Equal(1240m / 25m, await _prueba.ConsultarAsync(c => c.CostoPromedioAsync(producto, D3)));
    }

    [Fact]
    public async Task Consulta_DentroDeUnaTransaccion_VeLoRegistradoAunNoConfirmado()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();

        await using var scope = _prueba.Provider.CreateAsyncScope();
        var sesion = scope.ServiceProvider.GetRequiredService<IDbSession>();
        var registro = scope.ServiceProvider.GetRequiredService<IRegistroMovimientosInventario>();
        var consulta = scope.ServiceProvider.GetRequiredService<IConsultaInventario>();
        await using var tx = await sesion.BeginTransactionAsync();

        await registro.RegistrarAsync(LibroInventarioPrueba.Entrada(producto, almacen, 3m, 2m, D1));

        Assert.Equal(3m, await consulta.ExistenciaAsync(producto, almacen, null));
        await sesion.RollbackAsync();

        await using var conexion = _prueba.NuevaConexion();
        Assert.Equal(0L, await conexion.QuerySingleAsync<long>(
            """SELECT COUNT(*) FROM "MovimientosProducto" WHERE "ProductoId" = @p""", new { p = producto }));
    }
}
