using Dapper;
using Microsoft.Extensions.DependencyInjection;
using OpenSource1.Application.Features.Inventario.Consultas;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Services;

/// <summary>
/// Proyección <c>Producto.CostoUnitario</c> (Task 8.3) contra Postgres real: tras cada movimiento registrado por
/// <c>IRegistroMovimientosInventario</c> vale el promedio vigente a la última fecha con movimientos del producto
/// (<c>V / Q</c> sobre TODOS sus movimientos de valor, redondeado a 4) si <c>Q &gt; 0</c>, y conserva su valor si
/// <c>Q &lt;= 0</c>; <c>AjustarCostoMovimientos</c> la deja con la misma definición. REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ProyeccionCostoUnitarioTests(PostgresTestFixture fixture)
    : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private static readonly DateOnly D1 = new(2026, 5, 1);
    private static readonly DateOnly D2 = new(2026, 5, 2);
    private static readonly DateOnly D3 = new(2026, 5, 3);
    private static readonly DateOnly D4 = new(2026, 5, 4);
    private static readonly DateOnly FechaCorte = new(2099, 12, 31);

    private readonly LibroInventarioPrueba _prueba = new(fixture);

    public Task InitializeAsync() => _prueba.InicializarAsync();

    public async Task DisposeAsync() => await _prueba.DisposeAsync();

    [Fact]
    public async Task EntradaACostoNuevo_ActualizaElCostoUnitarioAlPromedio()
    {
        var producto = await _prueba.SembrarProductoAsync(costoUnitario: 3m);
        var almacen = await _prueba.SembrarAlmacenAsync();

        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 10m, D1));
        Assert.Equal(10m, await CostoUnitarioAsync(producto));

        // (100 + 200) / 20 = 15.
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 20m, D2));
        Assert.Equal(15m, await CostoUnitarioAsync(producto));

        // Con decimales: (300 + 3 × 1.0001) / 23 = 303.0003 / 23 = 13.173926… → 13.1739.
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 3m, 1.0001m, D2));
        Assert.Equal(13.1739m, await CostoUnitarioAsync(producto));
    }

    [Fact]
    public async Task Salida_ElCostoUnitarioSigueSiendoElPromedio()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 3m, 10m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 7m, 11.1111m, D1));
        Assert.Equal(10.7778m, await CostoUnitarioAsync(producto)); // 107.7777 / 10

        var salida = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 4m, D2));

        // 4 × 10.77777 = 43.11108 → −43.1111; quedan 64.6666 / 6 = 10.77776… → 10.7778.
        Assert.Equal(-43.1111m, salida.ImporteCosto);
        Assert.Equal(10.7778m, await CostoUnitarioAsync(producto));
    }

    [Fact]
    public async Task SalidaTotal_QSinCantidad_ConservaElUltimoCostoUnitario()
    {
        var producto = await _prueba.SembrarProductoAsync(costoUnitario: 3m);
        var almacen = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 12m, D1));

        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 10m, D2));

        Assert.Equal(0m, await CantidadValoradaAsync(producto));
        Assert.Equal(12m, await CostoUnitarioAsync(producto));
    }

    [Fact]
    public async Task EntradaRetroactiva_ProyeccionCoherenteConLaUltimaFecha_YTrasElAjusteCoincideConLaVista()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 10m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 5m, D3));
        Assert.Equal(10m, await CostoUnitarioAsync(producto));

        // Retroactiva (D2 < D3): el promedio a la última fecha (D3) es V / Q sobre todo el libro: (100 − 50 + 200) / 15 =
        // 16.6667, no el costo de la entrada ni el promedio de D2 (15). La salida de D3 sigue a −50 hasta el ajuste.
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 20m, D2));
        Assert.Equal(16.6667m, await CostoUnitarioAsync(producto));
        Assert.Equal(16.6667m, await CostoMedioVistaAsync(producto, almacen));

        // El ajuste revalora la salida a −75: (100 + 200 − 75) / 15 = 15, igual que la vista de existencias.
        Assert.Equal(new(1, 1), await _prueba.AjustarOkAsync(producto));
        Assert.Equal(15m, await CostoUnitarioAsync(producto));
        Assert.Equal(15m, await CostoMedioVistaAsync(producto, almacen));
    }

    [Fact]
    public async Task ProductoAgotadoTrasEntradaRetroactiva_ElAjusteDejaElUltimoPromedioAjustado_NoElConservadoPorElRegistro()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 10m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 5m, D3));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 20m, D2));
        Assert.Equal(16.6667m, await CostoUnitarioAsync(producto)); // (100 − 50 + 200) / 15

        // Agota el producto: Q = 0 → el registro conserva 16.6667 (calculado con la salida de D3 aún a −50).
        var salidaD4 = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 15m, D4));
        Assert.Equal(-250m, salidaD4.ImporteCosto);
        Assert.Equal(0m, await CantidadValoradaAsync(producto));
        Assert.Equal(16.6667m, await CostoUnitarioAsync(producto));

        // El ajuste revalora D3 a −75 (promedio 300 / 20 = 15) y D4 a −225 (225 / 15 = 15): Q sigue en 0 y la proyección
        // toma el último promedio ajustado, 15.
        Assert.Equal(new(1, 2), await _prueba.AjustarOkAsync(producto));
        Assert.Equal(0m, await CantidadValoradaAsync(producto));
        Assert.Equal(15m, await CostoUnitarioAsync(producto));
    }

    [Fact]
    public async Task TrasRegistrarYDespuesAjustar_SinNadaQueAjustar_ElCostoUnitarioCoincide()
    {
        // Entradas que suman 10 en 3 unidades y una salida: el promedio DEL DÍA de la salida es 3.33333… (lo que usa la
        // calculadora del posteo), pero tras ella quedan 6.6667 / 2 = 3.33335 → 3.3334. Registro y ajuste dan lo mismo
        // (V / Q sobre todo el libro); el ajuste no inserta nada.
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 1m, 3.3333m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 1m, 3.3333m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 1m, 3.3334m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 1m, D2));
        var trasRegistrar = await CostoUnitarioAsync(producto);
        Assert.Equal(3.3334m, trasRegistrar);

        Assert.Equal(new(1, 0), await _prueba.AjustarOkAsync(producto));

        Assert.Equal(trasRegistrar, await CostoUnitarioAsync(producto));
        Assert.Equal(trasRegistrar, await CostoMedioVistaAsync(producto, almacen));
    }

    [Fact]
    public async Task VariosMovimientosEnUnAlmacen_LaVistaDeExistenciasCoincideConLaProyeccionTrasCadaUno()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        var otro = await _prueba.SembrarAlmacenAsync();

        async Task ComprobarAsync()
        {
            var vista = await CostoMedioVistaAsync(producto, almacen);
            Assert.Equal(vista, await CostoUnitarioAsync(producto));
        }

        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 7m, 3.1234m, D1));
        await ComprobarAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 5m, 4.9999m, D2));
        await ComprobarAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 3m, D2));
        await ComprobarAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 2m, 1.5m, D1)); // retroactiva
        await ComprobarAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 4m, D3));
        await ComprobarAsync();

        // Ida y vuelta por transferencia: el promedio no cambia y la existencia vuelve al mismo almacén.
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 2m, D3, tipo: TipoMovimientoInventario.Transferencia));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, otro, 2m, null, D3, tipo: TipoMovimientoInventario.Transferencia));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, otro, 2m, D3, tipo: TipoMovimientoInventario.Transferencia));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 2m, null, D3, tipo: TipoMovimientoInventario.Transferencia));
        await ComprobarAsync();

        await _prueba.AjustarOkAsync(producto);
        await ComprobarAsync();
    }

    [Fact]
    public async Task ProductoSinMovimientos_ConservaSuValor_AunqueOtrosProductosSeMuevan()
    {
        var quieto = await _prueba.SembrarProductoAsync(costoUnitario: 7.25m);
        var movido = await _prueba.SembrarProductoAsync(costoUnitario: 1m);
        var almacen = await _prueba.SembrarAlmacenAsync();

        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(movido, almacen, 4m, 9m, D1));
        var fallida = await _prueba.RegistrarAsync(LibroInventarioPrueba.Salida(quieto, almacen, 1m, D1));
        Assert.True(fallida.EsFallo);

        Assert.Equal(7.25m, await CostoUnitarioAsync(quieto));
        Assert.Equal(9m, await CostoUnitarioAsync(movido));

        // El ajuste de un producto sin movimientos tampoco lo toca.
        await _prueba.AjustarOkAsync(quieto);
        Assert.Equal(7.25m, await CostoUnitarioAsync(quieto));
    }

    [Fact]
    public async Task MovimientoQueNoCambiaElPromedio_NoTocaLaFilaDelProducto()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 10m, D1));
        var xmin = await XminAsync(producto);

        // Mismo costo: el promedio sigue en 10 y el producto no tiene salidas (no se marca ajuste): ni un UPDATE.
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 5m, 10m, D2));

        Assert.Equal(10m, await CostoUnitarioAsync(producto));
        Assert.Equal(xmin, await XminAsync(producto));
    }

    private async Task<decimal> CostoUnitarioAsync(Guid productoId)
    {
        await using var conexion = _prueba.NuevaConexion();
        return await conexion.ExecuteScalarAsync<decimal>(
            """SELECT "CostoUnitario" FROM "Productos" WHERE "Id" = @productoId""", new { productoId });
    }

    private async Task<decimal> CantidadValoradaAsync(Guid productoId)
    {
        await using var conexion = _prueba.NuevaConexion();
        return await conexion.ExecuteScalarAsync<decimal>(
            """SELECT COALESCE(SUM("CantidadValorada"), 0) FROM "MovimientosValor" WHERE "ProductoId" = @productoId""",
            new { productoId });
    }

    private async Task<string> XminAsync(Guid productoId)
    {
        await using var conexion = _prueba.NuevaConexion();
        return await conexion.ExecuteScalarAsync<string>(
            """SELECT xmin::text FROM "Productos" WHERE "Id" = @productoId""", new { productoId }) ?? string.Empty;
    }

    /// <summary>
    /// Costo medio de la fila de la vista de existencias (Task 7.2) a 4 decimales: valor / existencia de la fila, redondeado
    /// una sola vez (el <c>CostoMedio</c> que muestra la vista va a 6 decimales; redondearlo otra vez a 4 podría diferir en
    /// la cuarta cifra).
    /// </summary>
    private async Task<decimal> CostoMedioVistaAsync(Guid productoId, Guid almacenId)
    {
        await using var scope = _prueba.Provider.CreateAsyncScope();
        var repositorio = scope.ServiceProvider.GetRequiredService<IInventarioConsultasReadRepository>();
        var respuesta = await repositorio.ListExistenciasAsync(
            new ExistenciaVistaCriterios(AlmacenId: almacenId, ProductoId: productoId, Fecha: FechaCorte), new PageRequest());
        var fila = Assert.Single(respuesta.Pagina.Items);
        Assert.NotNull(fila.CostoMedio);
        var costo = Math.Round(fila.Valor / fila.Existencia, 4, MidpointRounding.AwayFromZero);
        Assert.True(Math.Abs(costo - fila.CostoMedio.Value) <= 0.00005m, $"{costo} vs CostoMedio {fila.CostoMedio}");
        return costo;
    }
}
