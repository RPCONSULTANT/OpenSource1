using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenSource1.Application.Data;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Services;

/// <summary>
/// <see cref="IAjusteCostoInventario"/> contra Postgres real con los triggers append-only activos (Task 3.5): un UPDATE
/// o DELETE sobre <c>MovimientosValor</c> haría fallar cualquiera de estos tests con P0001. REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AjusteCostoInventarioTests(PostgresTestFixture fixture)
    : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private static readonly DateOnly D1 = new(2026, 4, 1);
    private static readonly DateOnly D2 = new(2026, 4, 2);
    private static readonly DateOnly D3 = new(2026, 4, 3);
    private static readonly DateOnly D4 = new(2026, 4, 4);

    private readonly LibroInventarioPrueba _prueba = new(fixture);

    public Task InitializeAsync() => _prueba.InicializarAsync();

    public async Task DisposeAsync() => await _prueba.DisposeAsync();

    [Fact]
    public async Task EntradaRetroactiva_InsertaExactamenteUnDelta_YLaSegundaEjecucionNoInsertaNada()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 10m, D1));
        var salida = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 5m, D3));
        Assert.Equal(-50m, salida.ImporteCosto);

        // Retroactiva: D2 < D3 y a otro costo. El promedio de D3 pasa a (100 + 200) / 20 = 15 → la salida debería costar
        // −75; lleva −50 → delta −25.
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 20m, D2));
        Assert.False(await CostoAjustadoAsync(producto));

        var filasAntes = await _prueba.ContarFilasAsync(producto);
        var instantaneaAntes = await InstantaneaValorAsync(producto);

        var primera = await _prueba.AjustarOkAsync(producto);

        Assert.Equal(new ResultadoAjusteCosto(1, 1), primera);
        var filasTras1 = await _prueba.ContarFilasAsync(producto);
        Assert.Equal(filasAntes with { Valor = filasAntes.Valor + 1 }, filasTras1);

        // Append-only: las filas previas siguen idénticas (columna a columna).
        Assert.Equal(instantaneaAntes, await InstantaneaValorAsync(producto, hastaId: instantaneaAntes.MaxId));

        var ajuste = await AjustesAsync(producto);
        var fila = Assert.Single(ajuste);
        Assert.Equal(salida.MovimientoProductoId, fila.MovimientoProductoId);
        Assert.Equal(-25m, fila.ImporteCosto);
        Assert.Equal(0m, fila.CantidadValorada);
        Assert.Equal((short)TipoValor.CostoDirecto, fila.TipoValor);
        Assert.Equal((short)TipoOrigenMovimiento.AjusteCosto, fila.TipoOrigen);
        Assert.Equal($"AJUSTE-{salida.MovimientoProductoId}", fila.ClaveOrigen);
        Assert.Equal(D3, fila.FechaRegistro);
        Assert.Equal(almacen, fila.AlmacenId);
        Assert.Equal((short)TipoMovimientoInventario.AjusteNegativo, fila.TipoMovimiento);
        Assert.Equal(0m, fila.ImporteVenta);

        Assert.True(await CostoAjustadoAsync(producto));
        Assert.Equal(15m, await CostoUnitarioAsync(producto));

        // Idempotencia: aunque se fuerce el producto (productoId explícito), no hay nada más que insertar.
        var segunda = await _prueba.AjustarOkAsync(producto);
        Assert.Equal(0, segunda.MovimientosValorCreados);
        Assert.Equal(filasTras1, await _prueba.ContarFilasAsync(producto));
    }

    [Fact]
    public async Task TrasAjustar_CostoUnitarioEsElPromedioFinalYElValorEsExistenciaPorCosto()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 3m, 10m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 7m, 11.1111m, D2));
        // Promedio D3 = 107.7777 / 10 = 10.77777 → 4 × 10.77777 = 43.11108 → −43.1111.
        var salida = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 4m, D3));
        Assert.Equal(-43.1111m, salida.ImporteCosto);

        // Retroactiva a D1: promedio D3 = 127.7777 / 15 = 8.5185133… → 4 × … = 34.0740533… → −34.0741; delta +9.0370.
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 5m, 4m, D1));

        var resultado = await _prueba.AjustarOkAsync(producto);

        Assert.Equal(new ResultadoAjusteCosto(1, 1), resultado);
        Assert.Equal(9.037m, Assert.Single(await AjustesAsync(producto)).ImporteCosto);
        Assert.True(await CostoAjustadoAsync(producto));

        var costo = await CostoUnitarioAsync(producto);
        Assert.Equal(8.5185m, costo);

        var valor = await ValorTotalAsync(producto);
        var existencia = await _prueba.ConsultarAsync(c => c.ExistenciaAsync(producto, null, null));
        Assert.Equal(93.7036m, valor);
        Assert.Equal(11m, existencia);
        Assert.True(Math.Abs(valor - existencia * costo) <= 0.0001m, $"valor {valor} vs {existencia} × {costo}");
    }

    [Fact]
    public async Task Redondeo_TresEntradasQueSumanDiezYSalidaTotal_DejaValorCeroConUnaFilaDeRedondeo()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        // 3 unidades por 10.0000: costo 3.3333… ; cada salida de 1 cuesta 3.3333 y las tres dejan 0.0001 de residuo.
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 1m, 3.3333m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 1m, 3.3333m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 1m, 3.3334m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 1m, D2));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 1m, D2));
        var ultima = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 1m, D2));
        Assert.Equal(0.0001m, await ValorTotalAsync(producto));

        var resultado = await _prueba.AjustarOkAsync(producto);

        Assert.Equal(new ResultadoAjusteCosto(1, 1), resultado);
        Assert.Equal(0m, await ValorTotalAsync(producto));
        var fila = Assert.Single(await AjustesAsync(producto));
        Assert.Equal((short)TipoValor.Redondeo, fila.TipoValor);
        Assert.Equal(-0.0001m, fila.ImporteCosto);
        Assert.Equal(0m, fila.CantidadValorada);
        Assert.Equal(ultima.MovimientoProductoId, fila.MovimientoProductoId);
        Assert.Equal(D2, fila.FechaRegistro);
        // Task 8.3: con Q = 0 al final, el ajuste usa su último promedio ajustado con Q > 0 (el de D2: 10 / 3 → 3.3333), no
        // la proyección que conservó el registro (tras la primera salida, 6.6667 / 2 = 3.33335 → 3.3334).
        Assert.Equal(3.3333m, await CostoUnitarioAsync(producto));

        var filas = await _prueba.ContarFilasAsync(producto);
        Assert.Equal(0, (await _prueba.AjustarOkAsync(producto)).MovimientosValorCreados);
        Assert.Equal(filas, await _prueba.ContarFilasAsync(producto));
        Assert.Equal(0m, await ValorTotalAsync(producto));
    }

    [Fact]
    public async Task EntradaDelMismoDiaPosteriorALaSalida_EntraEnSuPromedio_YCoincideConLaCalculadoraDelPosteo()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 10m, D1));
        var salida = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 5m, D2));
        // Mismo día, Id mayor que la salida: las salidas del día comparten el costo con TODAS las entradas del día.
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 20m, D2));

        Assert.Equal(new ResultadoAjusteCosto(1, 1), await _prueba.AjustarOkAsync(producto));
        var ajuste = Assert.Single(await AjustesAsync(producto));
        Assert.Equal(salida.MovimientoProductoId, ajuste.MovimientoProductoId);
        Assert.Equal(-25m, ajuste.ImporteCosto);

        // Coherencia: una salida posteada ahora en D2 con la calculadora sale al mismo costo (15) que la ajustada, y la
        // rutina no tiene nada que corregirle.
        var otra = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 2m, D2));
        Assert.Equal(-30m, otra.ImporteCosto);
        Assert.Equal(new ResultadoAjusteCosto(1, 0), await _prueba.AjustarOkAsync(producto));
    }

    [Fact]
    public async Task Transferencia_SalidaYEntradaSeRevaloranAlPromedioDelDia_SinCambiarElValorTotal()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var origen = await _prueba.SembrarAlmacenAsync();
        var destino = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, origen, 10m, 10m, D1));
        var sale = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(
            producto, origen, 4m, D3, tipo: TipoMovimientoInventario.Transferencia));
        var entra = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(
            producto, destino, 4m, null, D3, tipo: TipoMovimientoInventario.Transferencia));
        Assert.Equal(-40m, sale.ImporteCosto);
        Assert.Equal(40m, entra.ImporteCosto);

        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, origen, 10m, 20m, D2));

        Assert.Equal(new ResultadoAjusteCosto(1, 2), await _prueba.AjustarOkAsync(producto));
        var ajustes = (await AjustesAsync(producto)).ToDictionary(a => a.MovimientoProductoId!.Value, a => a.ImporteCosto);
        Assert.Equal(-20m, ajustes[sale.MovimientoProductoId]);
        Assert.Equal(20m, ajustes[entra.MovimientoProductoId]);
        Assert.Equal(300m, await ValorTotalAsync(producto));
        Assert.Equal(15m, await CostoUnitarioAsync(producto));

        // Una salida posteada ahora en D3 desde el destino cuesta el promedio ajustado; la rutina no la toca.
        var otra = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, destino, 3m, D3));
        Assert.Equal(-45m, otra.ImporteCosto);
        Assert.Equal(new ResultadoAjusteCosto(1, 0), await _prueba.AjustarOkAsync(producto));
    }

    [Fact]
    public async Task SinProductoIndicado_AjustaLosPendientes_IncluidoUnProductoBorradoLogicamente()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 10m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 5m, D3));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 20m, D2));
        await using (var conexion = _prueba.NuevaConexion())
        {
            await conexion.ExecuteAsync("""UPDATE "Productos" SET "IsDeleted" = true WHERE "Id" = @p""", new { p = producto });
        }

        var resultado = await _prueba.AjustarOkAsync(null);

        Assert.True(resultado.ProductosAjustados >= 1);
        Assert.True(resultado.MovimientosValorCreados >= 1);
        Assert.Equal(-25m, Assert.Single(await AjustesAsync(producto)).ImporteCosto);
        Assert.True(await CostoAjustadoAsync(producto));

        // Ya no queda ningún pendiente en toda la base: una segunda pasada global no inserta nada.
        Assert.Equal(new ResultadoAjusteCosto(0, 0), await _prueba.AjustarOkAsync(null));
    }

    [Fact]
    public async Task StockNegativoLegado_CompraPosteriorQueLoCompensa_ValoraLaSalidaAlCostoDeEsaCompraYDejaValorCero()
    {
        // (a) Salida de apertura −5 a 7 en D1 (el Stock negativo que migra la Task 3.6) y compra de 5 a 20 en D2: D1 no tiene
        // costo calculable (Q <= 0) → toma el del pool de D2 (20): esperado −100, actual −35 → delta −65.
        var producto = await _prueba.SembrarProductoAsync(costoUnitario: 7m);
        var almacen = await _prueba.SembrarAlmacenAsync();
        var apertura = await InsertarSalidaDeAperturaAsync(producto, almacen, 5m, 7m, D1);
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 5m, 20m, D2));

        Assert.Equal(new ResultadoAjusteCosto(1, 1), await _prueba.AjustarOkAsync(producto));

        var ajuste = Assert.Single(await AjustesAsync(producto));
        Assert.Equal(apertura, ajuste.MovimientoProductoId);
        Assert.Equal(-65m, ajuste.ImporteCosto);
        Assert.Equal(0m, await ValorTotalAsync(producto));
        Assert.True(await CostoAjustadoAsync(producto));

        var filas = await _prueba.ContarFilasAsync(producto);
        Assert.Equal(new ResultadoAjusteCosto(1, 0), await _prueba.AjustarOkAsync(producto));
        Assert.Equal(filas, await _prueba.ContarFilasAsync(producto));
    }

    [Fact]
    public async Task StockNegativoLegado_CompraMayor_PromedioFinalEsElDeLaCompra()
    {
        // (b) −5 a 7 en D1, +10 a 20 en D2 → la salida vale −100 (delta −65); quedan 5 unidades por 100 → promedio 20.
        var producto = await _prueba.SembrarProductoAsync(costoUnitario: 7m);
        var almacen = await _prueba.SembrarAlmacenAsync();
        await InsertarSalidaDeAperturaAsync(producto, almacen, 5m, 7m, D1);
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 20m, D2));

        Assert.Equal(new ResultadoAjusteCosto(1, 1), await _prueba.AjustarOkAsync(producto));

        Assert.Equal(-65m, Assert.Single(await AjustesAsync(producto)).ImporteCosto);
        Assert.Equal(20m, await CostoUnitarioAsync(producto));
        Assert.Equal(5m, await _prueba.ConsultarAsync(c => c.ExistenciaAsync(producto, null, null)));
        Assert.Equal(100m, await ValorTotalAsync(producto));
        Assert.Equal(new ResultadoAjusteCosto(1, 0), await _prueba.AjustarOkAsync(producto));
    }

    [Fact]
    public async Task StockNegativoLegadoSinEntradasNiHistoria_ConservaElImporteYQuedaPendiente_HastaQueLlegaUnaEntrada()
    {
        // (c) Solo −5 a 7: sin días posteriores con entradas ni promedio previo → conserva −35 y CostoAjustado sigue false.
        var producto = await _prueba.SembrarProductoAsync(costoUnitario: 7m);
        var almacen = await _prueba.SembrarAlmacenAsync();
        await InsertarSalidaDeAperturaAsync(producto, almacen, 5m, 7m, D1);
        var filas = await _prueba.ContarFilasAsync(producto);

        Assert.Equal(new ResultadoAjusteCosto(0, 0), await _prueba.AjustarOkAsync(producto));
        Assert.Equal(-35m, await ValorTotalAsync(producto));
        Assert.False(await CostoAjustadoAsync(producto));

        Assert.Equal(new ResultadoAjusteCosto(0, 0), await _prueba.AjustarOkAsync(producto));
        Assert.Equal(filas, await _prueba.ContarFilasAsync(producto));
        Assert.False(await CostoAjustadoAsync(producto));

        // Llega la entrada (el posteo la marca pendiente: el producto tiene salidas; ya lo estaba) y la siguiente
        // pasada lo resuelve.
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 5m, 20m, D2));
        Assert.False(await CostoAjustadoAsync(producto));
        Assert.Equal(new ResultadoAjusteCosto(1, 1), await _prueba.AjustarOkAsync(producto));
        Assert.Equal(0m, await ValorTotalAsync(producto));
        Assert.True(await CostoAjustadoAsync(producto));
    }

    [Fact]
    public async Task EntradaIntermediaPosteriorAlAjuste_DejaPendiente_YLaPasadaGlobalRevaloraElDiaSinCosto()
    {
        // Ruling AS: −5 a 7 en D1, +5 a 20 en D3 → ajuste (salida −100). Después se postea +5 a 30 con fecha D2: cambia el
        // pool que valora D1 (ahora 30 → −150), así que el posteo debe dejar el producto pendiente aunque D2 > D1, y la
        // pasada GLOBAL lo recoge e inserta −50.
        var producto = await _prueba.SembrarProductoAsync(costoUnitario: 7m);
        var almacen = await _prueba.SembrarAlmacenAsync();
        var apertura = await InsertarSalidaDeAperturaAsync(producto, almacen, 5m, 7m, D1);
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 5m, 20m, D3));
        Assert.Equal(new ResultadoAjusteCosto(1, 1), await _prueba.AjustarOkAsync(producto));
        Assert.True(await CostoAjustadoAsync(producto));

        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 5m, 30m, D2));
        Assert.False(await CostoAjustadoAsync(producto));

        var global = await _prueba.AjustarOkAsync(null);

        Assert.True(global.MovimientosValorCreados >= 1);
        var ajustes = await AjustesAsync(producto);
        Assert.Equal([-65m, -50m], ajustes.Select(a => a.ImporteCosto));
        Assert.All(ajustes, a => Assert.Equal(apertura, a.MovimientoProductoId));
        Assert.True(await CostoAjustadoAsync(producto));

        var costo = await CostoUnitarioAsync(producto);
        var existencia = await _prueba.ConsultarAsync(c => c.ExistenciaAsync(producto, null, null));
        Assert.Equal(20m, costo);
        Assert.Equal(5m, existencia);
        Assert.Equal(existencia * costo, await ValorTotalAsync(producto));

        var filas = await _prueba.ContarFilasAsync(producto);
        Assert.Equal(0, (await _prueba.AjustarOkAsync(null)).MovimientosValorCreados);
        Assert.Equal(filas, await _prueba.ContarFilasAsync(producto));
    }

    [Fact]
    public async Task DiaSinCostoCalculableConHistoriaPrevia_TomaElCostoDeLaSiguienteEntrada_NoElUltimoPromedio()
    {
        // (d) Entrada 10 a 10 (D1); salida 15 en D2 (Q del día = 10 → costo 10, deja Q = −5); salida 2 en D3 (Q <= 0: no
        // calculable) y entrada a 30 en D4 → la salida de D3 toma 30 (no el último promedio, 10): −60, delta −40.
        var producto = await _prueba.SembrarProductoAsync(costoUnitario: 10m);
        var almacen = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 10m, D1));
        await InsertarSalidaDeAperturaAsync(producto, almacen, 15m, 10m, D2);
        var salidaD3 = await InsertarSalidaDeAperturaAsync(producto, almacen, 2m, 10m, D3);
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 30m, D4));

        Assert.Equal(new ResultadoAjusteCosto(1, 1), await _prueba.AjustarOkAsync(producto));

        var ajuste = Assert.Single(await AjustesAsync(producto));
        Assert.Equal(salidaD3, ajuste.MovimientoProductoId);
        Assert.Equal(-40m, ajuste.ImporteCosto);
        Assert.True(await CostoAjustadoAsync(producto));
        Assert.Equal(new ResultadoAjusteCosto(1, 0), await _prueba.AjustarOkAsync(producto));
    }

    [Fact]
    public async Task ConTransaccionActiva_FallaSinEscribir()
    {
        var producto = await _prueba.SembrarProductoAsync();

        await using var scope = _prueba.Provider.CreateAsyncScope();
        var sesion = scope.ServiceProvider.GetRequiredService<IDbSession>();
        await using var tx = await sesion.BeginTransactionAsync();
        var resultado = await scope.ServiceProvider.GetRequiredService<IAjusteCostoInventario>().AjustarAsync(producto);

        Assert.True(resultado.EsFallo);
        Assert.Equal("inventario.ajuste_en_transaccion", resultado.Errores[0].Codigo);
    }

    [Fact]
    public async Task ProductoInexistente_FallaConReferenciaInvalida()
    {
        var resultado = await _prueba.AjustarAsync(Guid.NewGuid());

        Assert.True(resultado.EsFallo);
        Assert.Equal("inventario.producto_invalido", resultado.Errores[0].Codigo);
    }

    private sealed record FilaAjuste(
        long Id, long? MovimientoProductoId, Guid AlmacenId, short TipoValor, short TipoMovimiento, DateOnly FechaRegistro,
        decimal CantidadValorada, decimal ImporteCosto, decimal ImporteVenta, short TipoOrigen, string ClaveOrigen);

    private async Task<List<FilaAjuste>> AjustesAsync(Guid productoId)
    {
        await using var conexion = _prueba.NuevaConexion();
        return (await conexion.QueryAsync<FilaAjuste>(
            """
            SELECT "Id", "MovimientoProductoId", "AlmacenId", "TipoValor", "TipoMovimiento", "FechaRegistro",
                   "CantidadValorada", "ImporteCosto", "ImporteVenta", "TipoOrigen", "ClaveOrigen"
            FROM "MovimientosValor" WHERE "ProductoId" = @p AND "Ajuste" ORDER BY "Id"
            """,
            new { p = productoId })).ToList();
    }

    private async Task<(long MaxId, string Filas)> InstantaneaValorAsync(Guid productoId, long hastaId = long.MaxValue)
    {
        await using var conexion = _prueba.NuevaConexion();
        return await conexion.QuerySingleAsync<(long, string)>(
            """
            SELECT MAX(v."Id"), string_agg(to_jsonb(v)::text, '|' ORDER BY v."Id")
            FROM "MovimientosValor" v WHERE v."ProductoId" = @p AND v."Id" <= @hastaId
            """,
            new { p = productoId, hastaId });
    }

    private async Task<decimal> ValorTotalAsync(Guid productoId)
    {
        await using var conexion = _prueba.NuevaConexion();
        return await conexion.ExecuteScalarAsync<decimal>(
            """SELECT COALESCE(SUM("ImporteCosto"), 0) FROM "MovimientosValor" WHERE "ProductoId" = @p""", new { p = productoId });
    }

    private async Task<bool> CostoAjustadoAsync(Guid productoId)
    {
        await using var contexto = _prueba.NuevoContexto();
        return await contexto.Productos.IgnoreQueryFilters().Where(p => p.Id == productoId)
            .Select(p => p.CostoAjustado).SingleAsync();
    }

    private async Task<decimal> CostoUnitarioAsync(Guid productoId)
    {
        await using var contexto = _prueba.NuevoContexto();
        return await contexto.Productos.IgnoreQueryFilters().Where(p => p.Id == productoId)
            .Select(p => p.CostoUnitario).SingleAsync();
    }

    /// <summary>Salida sin aplicaciones ni existencia previa (el caso del Stock negativo migrado) y marca el producto pendiente.</summary>
    private async Task<long> InsertarSalidaDeAperturaAsync(Guid productoId, Guid almacenId, decimal cantidad, decimal costo, DateOnly fecha)
    {
        await using var conexion = _prueba.NuevaConexion();
        var clave = $"APERTURA-{Guid.NewGuid():N}"[..40];
        var movimientoId = await conexion.ExecuteScalarAsync<long>(
            """
            INSERT INTO "MovimientosProducto" (
                "ProductoId", "AlmacenId", "TipoMovimiento", "TipoDocumento", "NumeroLineaDocumento", "FechaRegistro",
                "FechaDocumento", "Cantidad", "CantidadFacturada", "UnidadMedidaId", "CantidadPorUnidadMedida",
                "TipoOrigen", "ClaveOrigen", "CreatedAtUtc", "CreatedBy")
            VALUES (@productoId, @almacenId, 4, 1, 1, @fecha, @fecha, @cantidad, 0, @unidad, 1, 99, @clave, now(), 'test')
            RETURNING "Id"
            """,
            new { productoId, almacenId, fecha, cantidad = -cantidad, unidad = LibroInventarioPrueba.UnidadUnd, clave });
        await conexion.ExecuteAsync(
            """
            INSERT INTO "MovimientosValor" (
                "MovimientoProductoId", "ProductoId", "AlmacenId", "TipoValor", "TipoMovimiento", "FechaRegistro",
                "CantidadValorada", "CantidadFacturada", "ImporteCosto", "CostoPorUnidad", "ImporteVenta",
                "ImporteCostoPosteadoContabilidad", "Ajuste", "TipoDocumento", "NumeroLineaDocumento",
                "TipoOrigen", "ClaveOrigen", "CreatedAtUtc", "CreatedBy")
            VALUES (@movimientoId, @productoId, @almacenId, 1, 4, @fecha, @cantidad, 0, @importe, @costo, 0, 0, false, 1, 1,
                    99, @clave, now(), 'test')
            """,
            new { movimientoId, productoId, almacenId, fecha, cantidad = -cantidad, importe = -cantidad * costo, costo, clave });
        await conexion.ExecuteAsync(
            """UPDATE "Productos" SET "CostoAjustado" = false WHERE "Id" = @productoId""", new { productoId });
        return movimientoId;
    }
}
