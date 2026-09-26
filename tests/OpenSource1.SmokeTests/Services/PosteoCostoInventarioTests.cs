using Dapper;
using Microsoft.Extensions.DependencyInjection;
using OpenSource1.Application.Data;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Services;

/// <summary>
/// Batch <see cref="IPosteoCostoInventario"/> (Task 5.6) contra Postgres real, con los triggers append-only activos (el único
/// UPDATE que admite <c>MovimientosValor</c> es el de <c>ImporteCostoPosteadoContabilidad</c>) y el constraint trigger de cuadre
/// del libro contable. Los escenarios se siembran con el registro real del libro de inventario y la rutina real de ajuste de
/// costo. Todas las clases de la colección tienen su propio contenedor, pero los tests de ESTA clase comparten base: las
/// aserciones se hacen por producto (dimensión <c>ProductoId</c> de las líneas) o por los ids de movimiento del test.
/// REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PosteoCostoInventarioTests(PostgresTestFixture fixture)
    : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private static readonly DateOnly D1 = new(2026, 5, 1);
    private static readonly DateOnly D2 = new(2026, 5, 2);
    private static readonly DateOnly D3 = new(2026, 5, 3);
    private static readonly DateOnly D4 = new(2026, 5, 4);

    private readonly LibroInventarioPrueba _prueba = new(fixture);

    public Task InitializeAsync() => _prueba.InicializarAsync();

    public async Task DisposeAsync() => await _prueba.DisposeAsync();

    [Fact]
    public async Task AperturaAjustesYTransferenciaConLaMismaCuenta_AsientosCuadrados_YSaldoDeInventarioIgualAlValor()
    {
        var producto = await ProductoClasificadoAsync();
        var a = await _prueba.SembrarAlmacenAsync();
        var b = await _prueba.SembrarAlmacenAsync();

        // Apertura migrada (TipoOrigen = Migracion, como ReemplazarStockPorLibro): 10 × 10 = +100.
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, a, 10m, 10m, D1) with
        {
            TipoOrigen = TipoOrigenMovimiento.Migracion, ClaveOrigen = "MIGRACION-STOCK",
        });
        // D2: ajuste positivo 5 × 12 = +60 y ajuste negativo de 3 al promedio (160 / 15) → −32.
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, a, 5m, 12m, D2));
        var negativo = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, a, 3m, D2));
        Assert.Equal(-32m, negativo.ImporteCosto);
        // D3: transferencia A → B de 4 (las dos cuentas de inventario son la 1301 por el comodín): neto 0 por cuenta.
        var sale = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, a, 4m, D3, tipo: TipoMovimientoInventario.Transferencia));
        var entra = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, b, 4m, null, D3, tipo: TipoMovimientoInventario.Transferencia));
        Assert.Equal(0m, sale.ImporteCosto + entra.ImporteCosto);
        Assert.NotEqual(0m, sale.ImporteCosto);

        var resultado = await PostearAsync(producto);

        Assert.Empty(resultado.Pendientes);
        Assert.Equal(2, resultado.Asientos); // D1 y D2; D3 netea a 0 y no crea registro.
        Assert.Equal(5, resultado.MovimientosValorContabilizados);
        Assert.Equal(0, await PendientesDelProductoAsync(producto));

        var valor = await ValorInventarioAsync(producto);
        Assert.Equal(128m, valor);
        Assert.Equal(valor, await SaldoAsync(CuentaContableIds.Inventario, producto));
        Assert.Equal(-valor, await SaldoAsync(CuentaContableIds.AjusteInventario, producto));
        await AssertTodosLosRegistrosCuadranAsync();

        var lineas = await LineasAsync(producto);
        Assert.DoesNotContain(lineas, l => l.FechaRegistro == D3);
        // Agrupado por cuenta: D2 = una línea de 1301 (+28) y una de 5201 (−28), no cuatro.
        Assert.Equal(
            [("1301", 28m), ("5201", -28m)],
            lineas.Where(l => l.FechaRegistro == D2).OrderBy(l => l.NumeroCuenta).Select(l => (l.NumeroCuenta, l.Importe)));
        Assert.All(lineas, l => Assert.Equal((short)TipoOrigenMovimiento.CostoInventario, l.TipoOrigen));
        Assert.All(lineas, l => Assert.Equal((short)TipoDocumentoContable.CostoInventario, l.TipoDocumento));
        Assert.All(lineas, l => Assert.Equal(GrupoContableIds.ProductoBienes, l.GrupoProductoId));
    }

    [Fact]
    public async Task Venta_ContrapartidaEsCostoDeVentas()
    {
        var producto = await ProductoClasificadoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 10m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 2m, D2, tipo: TipoMovimientoInventario.Venta));

        var resultado = await PostearAsync(producto);

        Assert.Empty(resultado.Pendientes);
        Assert.Equal(2, resultado.Asientos);
        Assert.Equal(80m, await SaldoAsync(CuentaContableIds.Inventario, producto));
        Assert.Equal(20m, await SaldoAsync(CuentaContableIds.CostoVentas, producto));
        Assert.Equal(-100m, await SaldoAsync(CuentaContableIds.AjusteInventario, producto));
        Assert.Equal(
            [("1301", -20m), ("5101", 20m)],
            (await LineasAsync(producto)).Where(l => l.FechaRegistro == D2).OrderBy(l => l.NumeroCuenta).Select(l => (l.NumeroCuenta, l.Importe)));
    }

    [Fact]
    public async Task TransferenciaEntreAlmacenesConCuentasDistintas_ContabilizaInventarioContraInventario()
    {
        var producto = await ProductoClasificadoAsync();
        var a = await _prueba.SembrarAlmacenAsync();
        var b = await _prueba.SembrarAlmacenAsync();
        var cuentaB = await CrearCuentaAsync();
        await CrearSetupInventarioAsync(b, GrupoContableIds.InventarioGeneral, cuentaB.Id);

        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, a, 10m, 10m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, a, 4m, D2, tipo: TipoMovimientoInventario.Transferencia));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, b, 4m, null, D2, tipo: TipoMovimientoInventario.Transferencia));

        var resultado = await PostearAsync(producto);

        Assert.Empty(resultado.Pendientes);
        Assert.Equal(2, resultado.Asientos);
        Assert.Equal(3, resultado.MovimientosValorContabilizados);
        Assert.Equal(
            [("1301", -40m), (cuentaB.Numero, 40m)],
            (await LineasAsync(producto)).Where(l => l.FechaRegistro == D2).OrderBy(l => l.Importe).Select(l => (l.NumeroCuenta, l.Importe)));
        Assert.Equal(60m, await SaldoAsync(CuentaContableIds.Inventario, producto));
        Assert.Equal(40m, await SaldoAsync(cuentaB.Id, producto));
        Assert.Equal(-100m, await SaldoAsync(CuentaContableIds.AjusteInventario, producto));
        await AssertTodosLosRegistrosCuadranAsync();
    }

    [Fact]
    public async Task TransferenciaConUnLadoSinCuentaValida_NingunLadoSeContabiliza_YElRestoDelDiaSi()
    {
        var producto = await ProductoClasificadoAsync();
        var a = await _prueba.SembrarAlmacenAsync();
        var b = await _prueba.SembrarAlmacenAsync();
        var bloqueada = await CrearCuentaAsync(bloqueada: true);
        await CrearSetupInventarioAsync(b, GrupoContableIds.InventarioGeneral, bloqueada.Id);

        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, a, 10m, 10m, D1));
        var ajuste = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, a, 2m, 10m, D2));
        var sale = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, a, 4m, D2, tipo: TipoMovimientoInventario.Transferencia));
        var entra = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, b, 4m, null, D2, tipo: TipoMovimientoInventario.Transferencia));

        var resultado = await PostearAsync(producto);

        Assert.Equal(2, resultado.Asientos);
        Assert.Equal(2, resultado.MovimientosValorContabilizados);
        var pendientes = resultado.Pendientes.ToDictionary(p => p.MovimientoValorId);
        Assert.Equal(2, pendientes.Count);
        Assert.Equal("setup_contable.cuenta_invalida", pendientes[entra.MovimientoValorId].Codigo);
        Assert.Equal("contabilidad.transferencia_incompleta", pendientes[sale.MovimientoValorId].Codigo);
        Assert.Equal(2, await PendientesDelProductoAsync(producto));
        Assert.True(await ContabilizadoAsync(ajuste.MovimientoValorId));
        Assert.Equal(120m, await SaldoAsync(CuentaContableIds.Inventario, producto));
        await AssertTodosLosRegistrosCuadranAsync();
    }

    [Fact]
    public async Task ReviewFocus2_GrupoSinSetupQuedaPendiente_ElOtroProductoSeContabiliza_YNadaAMedias()
    {
        // P1: grupo de inventario sin fila en SetupsInventario ni comodín.
        var sinSetup = await CrearGrupoInventarioAsync();
        var p1 = await ProductoClasificadoAsync(grupoInventario: sinSetup.Id);
        // P2: normal.
        var p2 = await ProductoClasificadoAsync();
        // P3: grupo de producto sin setup general: sus ajustes se contabilizan, su venta no.
        var grupoProductoSinSetup = await CrearGrupoProductoAsync();
        var p3 = await ProductoClasificadoAsync(grupoProducto: grupoProductoSinSetup.Id);
        // P4: sin grupos (congela null) → grupo faltante.
        var p4 = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();

        var m1 = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(p1, almacen, 5m, 10m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(p2, almacen, 5m, 10m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(p3, almacen, 10m, 10m, D1));
        var negativoP3 = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(p3, almacen, 1m, D2));
        var ventaP3 = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(p3, almacen, 2m, D2, tipo: TipoMovimientoInventario.Venta));
        var m4 = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(p4, almacen, 1m, 10m, D1));

        var resultado = await PostearAsync(null);

        var pendientes = resultado.Pendientes.ToDictionary(p => p.MovimientoValorId);
        Assert.Equal("setup_contable.inexistente", pendientes[m1.MovimientoValorId].Codigo);
        Assert.Contains($"GrupoInventario={sinSetup.Codigo}", pendientes[m1.MovimientoValorId].Mensaje);
        Assert.Equal("setup_contable.inexistente", pendientes[ventaP3.MovimientoValorId].Codigo);
        Assert.Contains($"GrupoProducto={grupoProductoSinSetup.Codigo}", pendientes[ventaP3.MovimientoValorId].Mensaje);
        Assert.Equal("setup_contable.grupo_faltante", pendientes[m4.MovimientoValorId].Codigo);
        Assert.DoesNotContain(negativoP3.MovimientoValorId, pendientes.Keys);

        // P1 y P4: nada escrito, siguen pendientes.
        Assert.Empty(await LineasAsync(p1));
        Assert.Empty(await LineasAsync(p4));
        Assert.Equal(1, await PendientesDelProductoAsync(p1));
        Assert.Equal(1, await PendientesDelProductoAsync(p4));
        // P2 completo.
        Assert.Equal(0, await PendientesDelProductoAsync(p2));
        Assert.Equal(50m, await SaldoAsync(CuentaContableIds.Inventario, p2));
        // P3: entrada y ajuste negativo sí; la venta entera no (ni su línea de inventario).
        Assert.Equal(1, await PendientesDelProductoAsync(p3));
        Assert.Equal(90m, await SaldoAsync(CuentaContableIds.Inventario, p3));
        Assert.Equal(0m, await SaldoAsync(CuentaContableIds.CostoVentas, p3));
        Assert.False(await ContabilizadoAsync(ventaP3.MovimientoValorId));
        await AssertTodosLosRegistrosCuadranAsync();

        // Con el setup ya creado, la siguiente ejecución contabiliza lo pendiente de P1.
        await CrearSetupInventarioAsync(null, sinSetup.Id, CuentaContableIds.Inventario);
        var segunda = await PostearAsync(p1);
        Assert.Empty(segunda.Pendientes);
        Assert.Equal(1, segunda.Asientos);
        Assert.Equal(50m, await SaldoAsync(CuentaContableIds.Inventario, p1));
    }

    [Fact]
    public async Task ReviewFocus3_DosEjecucionesSeguidas_LaSegundaNoInsertaNada()
    {
        var producto = await ProductoClasificadoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 10m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 3m, D2, tipo: TipoMovimientoInventario.Venta));

        var primera = await PostearAsync(null);
        Assert.True(primera.Asientos >= 2);
        var filas = await FilasLibroContableAsync();

        var segunda = await PostearAsync(null);

        Assert.Equal(0, segunda.Asientos);
        Assert.Equal(0, segunda.MovimientosValorContabilizados);
        Assert.Equal(filas, await FilasLibroContableAsync());
        Assert.Equal(70m, await SaldoAsync(CuentaContableIds.Inventario, producto));
    }

    [Fact]
    public async Task ReviewFocus3_EjecucionesEnParalelo_NuncaContabilizanDosVecesElMismoDelta()
    {
        var producto = await ProductoClasificadoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        var dias = Enumerable.Range(0, 6).Select(i => D1.AddDays(i)).ToArray();
        foreach (var dia in dias)
        {
            await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 10m, dia));
            await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 1m, dia, tipo: TipoMovimientoInventario.Venta));
        }

        const int ejecuciones = 6;
        using var barrera = new Barrier(ejecuciones);
        var tareas = Enumerable.Range(0, ejecuciones).Select(_ => Task.Run(async () =>
        {
            await using var scope = _prueba.Provider.CreateAsyncScope();
            var posteo = scope.ServiceProvider.GetRequiredService<IPosteoCostoInventario>();
            await scope.ServiceProvider.GetRequiredService<IDbSession>().EnsureOpenAsync();
            barrera.SignalAndWait();
            return await posteo.PostearAsync(producto);
        })).ToArray();
        var resultados = await Task.WhenAll(tareas);

        Assert.All(resultados, r => Assert.Empty(r.Pendientes));
        Assert.Equal(dias.Length, resultados.Sum(r => r.Asientos));
        Assert.Equal(dias.Length * 2, resultados.Sum(r => r.MovimientosValorContabilizados));
        Assert.Equal(await ValorInventarioAsync(producto), await SaldoAsync(CuentaContableIds.Inventario, producto));
        Assert.Equal(dias.Length, await RegistrosDelProductoAsync(producto));
        Assert.Equal(0, await PendientesDelProductoAsync(producto));
        await AssertTodosLosRegistrosCuadranAsync();
    }

    [Fact]
    public async Task TrasAjustarCostoMovimientos_LaSiguienteEjecucionContabilizaSoloElDelta()
    {
        var producto = await ProductoClasificadoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 10m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 5m, D3, tipo: TipoMovimientoInventario.Venta));
        Assert.Equal(2, (await PostearAsync(producto)).Asientos);
        Assert.Equal(50m, await SaldoAsync(CuentaContableIds.CostoVentas, producto));

        // Entrada retroactiva a otro costo: el promedio de D3 pasa a 15 y la rutina inserta un ajuste de −25 sobre la venta.
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 20m, D2));
        Assert.Equal(1, (await _prueba.AjustarOkAsync(producto)).MovimientosValorCreados);
        var registrosAntes = await RegistrosDelProductoAsync(producto);

        var resultado = await PostearAsync(producto);

        Assert.Empty(resultado.Pendientes);
        Assert.Equal(2, resultado.Asientos); // D2 (la entrada) y D3 (solo el delta del ajuste).
        Assert.Equal(2, resultado.MovimientosValorContabilizados);
        Assert.Equal(registrosAntes + 2, await RegistrosDelProductoAsync(producto));
        Assert.Equal(75m, await SaldoAsync(CuentaContableIds.CostoVentas, producto));
        var d3 = (await LineasAsync(producto)).Where(l => l.FechaRegistro == D3).Select(l => (l.NumeroCuenta, l.Importe)).ToList();
        Assert.Contains(("5101", 25m), d3);
        Assert.Contains(("1301", -25m), d3);
        Assert.Equal(await ValorInventarioAsync(producto), await SaldoAsync(CuentaContableIds.Inventario, producto));
        Assert.Equal(225m, await ValorInventarioAsync(producto));
        Assert.Equal(0, (await PostearAsync(producto)).Asientos);
    }

    [Fact]
    public async Task RedondeoConUnaTransferenciaComoUltimaSalidaDelDia_CaeEnOtraSalida_YElBatchContabilizaTodo()
    {
        var producto = await ProductoClasificadoAsync();
        var a = await _prueba.SembrarAlmacenAsync();
        var b = await _prueba.SembrarAlmacenAsync();
        // D1: 3 unidades por 10.0000 (costo 3.3333…); cada salida de 1 cuesta 3.3333.
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, a, 1m, 3.3333m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, a, 1m, 3.3333m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, a, 1m, 3.3334m, D1));
        // D2: dos salidas de A, entrada de transferencia en B, salida de B y, la ÚLTIMA salida del día (por Id), la salida de
        // la transferencia desde A. Al cierre de D2 la existencia es 0 y queda un residuo de 0.0001.
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, a, 1m, D2));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, a, 1m, D2));
        var entra = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(
            producto, b, 1m, null, D2, tipo: TipoMovimientoInventario.Transferencia));
        var salidaB = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, b, 1m, D2));
        var sale = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(
            producto, a, 1m, D2, tipo: TipoMovimientoInventario.Transferencia));
        Assert.True(sale.MovimientoProductoId > salidaB.MovimientoProductoId);
        Assert.Equal(0m, sale.ImporteCosto + entra.ImporteCosto);
        Assert.Equal(0.0001m, await ValorInventarioAsync(producto));

        await _prueba.AjustarOkAsync(producto);

        // El redondeo no cae en la transferencia (dejaría sus partes sin sumar 0) sino en la última salida NO transferencia.
        await using (var conexion = _prueba.NuevaConexion())
        {
            var redondeos = (await conexion.QueryAsync<(long MovimientoProductoId, decimal ImporteCosto)>(
                """
                SELECT "MovimientoProductoId", "ImporteCosto" FROM "MovimientosValor"
                WHERE "ProductoId" = @producto AND "TipoValor" = @tipo
                """,
                new { producto, tipo = (short)TipoValor.Redondeo })).ToList();
            Assert.Equal((salidaB.MovimientoProductoId, -0.0001m), Assert.Single(redondeos));
        }

        Assert.Equal(0m, await ValorInventarioAsync(producto));

        var resultado = await PostearAsync(producto);

        Assert.Empty(resultado.Pendientes);
        Assert.Equal(0, await PendientesDelProductoAsync(producto));
        Assert.Equal(0m, await SaldoAsync(CuentaContableIds.Inventario, producto));
        await AssertTodosLosRegistrosCuadranAsync();
    }

    [Fact]
    public async Task ImporteYaPosteadoEnParte_ContabilizaSoloLaDiferencia()
    {
        var producto = await ProductoClasificadoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        var entrada = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 10m, D4));
        await using (var conexion = _prueba.NuevaConexion())
        {
            // El único UPDATE que el trigger admite en MovimientosValor: simula un posteo anterior de 40 sobre 100.
            await conexion.ExecuteAsync(
                """UPDATE "MovimientosValor" SET "ImporteCostoPosteadoContabilidad" = 40 WHERE "Id" = @Id""",
                new { Id = entrada.MovimientoValorId });
        }

        var resultado = await PostearAsync(producto);

        Assert.Equal(1, resultado.Asientos);
        Assert.Equal(60m, await SaldoAsync(CuentaContableIds.Inventario, producto));
        Assert.Equal(-60m, await SaldoAsync(CuentaContableIds.AjusteInventario, producto));
        Assert.True(await ContabilizadoAsync(entrada.MovimientoValorId));
    }

    [Fact]
    public async Task ConUnaTransaccionYaActiva_LanzaInvalidOperationException()
    {
        await using var scope = _prueba.Provider.CreateAsyncScope();
        var sesion = scope.ServiceProvider.GetRequiredService<IDbSession>();
        await using var tx = await sesion.BeginTransactionAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IPosteoCostoInventario>().PostearAsync(null));
    }

    // ── Siembra ─────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Producto con grupo de inventario GENERAL y de producto BIENES (o los indicados) ANTES de registrar: el registro congela los grupos.</summary>
    private async Task<Guid> ProductoClasificadoAsync(Guid? grupoInventario = null, Guid? grupoProducto = null)
    {
        var producto = await _prueba.SembrarProductoAsync();
        await using var conexion = _prueba.NuevaConexion();
        await conexion.ExecuteAsync(
            """UPDATE "Productos" SET "GrupoInventarioId" = @Gi, "GrupoProductoId" = @Gp WHERE "Id" = @Id""",
            new
            {
                Gi = grupoInventario ?? GrupoContableIds.InventarioGeneral,
                Gp = grupoProducto ?? GrupoContableIds.ProductoBienes,
                Id = producto,
            });
        return producto;
    }

    private async Task<CuentaContable> CrearCuentaAsync(bool bloqueada = false)
    {
        await using var contexto = _prueba.NuevoContexto();
        var cuenta = new CuentaContable
        {
            Numero = $"13{Random.Shared.Next(100_000, 999_999)}",
            Nombre = "Inventario de prueba",
            TipoCuenta = TipoCuentaContable.Posteo,
            TipoResultado = TipoResultadoCuenta.Balance,
            Bloqueada = bloqueada,
            CreatedBy = "test",
        };
        contexto.CuentasContables.Add(cuenta);
        await contexto.SaveChangesAsync();
        return cuenta;
    }

    private async Task CrearSetupInventarioAsync(Guid? almacenId, Guid grupoInventarioId, Guid cuentaInventarioId)
    {
        await using var contexto = _prueba.NuevoContexto();
        contexto.SetupsInventario.Add(new SetupInventario
        {
            AlmacenId = almacenId,
            GrupoInventarioId = grupoInventarioId,
            CuentaInventarioId = cuentaInventarioId,
            CuentaAjusteInventarioId = CuentaContableIds.AjusteInventario,
            CuentaVariacionCostoId = CuentaContableIds.AjusteInventario,
            CreatedBy = "test",
        });
        await contexto.SaveChangesAsync();
    }

    private async Task<GrupoInventario> CrearGrupoInventarioAsync()
    {
        await using var contexto = _prueba.NuevoContexto();
        var grupo = new GrupoInventario { Codigo = $"GI{Guid.NewGuid():N}"[..12].ToUpperInvariant(), Descripcion = "Sin setup", CreatedBy = "test" };
        contexto.GruposInventario.Add(grupo);
        await contexto.SaveChangesAsync();
        return grupo;
    }

    private async Task<GrupoProducto> CrearGrupoProductoAsync()
    {
        await using var contexto = _prueba.NuevoContexto();
        var grupo = new GrupoProducto { Codigo = $"GP{Guid.NewGuid():N}"[..12].ToUpperInvariant(), Descripcion = "Sin setup", CreatedBy = "test" };
        contexto.GruposProducto.Add(grupo);
        await contexto.SaveChangesAsync();
        return grupo;
    }

    // ── Ejecución y consultas ───────────────────────────────────────────────────────────────────────────────────────

    private async Task<ResultadoPosteoCostoInventario> PostearAsync(Guid? productoId)
    {
        await using var scope = _prueba.Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPosteoCostoInventario>().PostearAsync(productoId);
    }

    private async Task<decimal> SaldoAsync(Guid cuentaId, Guid productoId)
    {
        await using var conexion = _prueba.NuevaConexion();
        return await conexion.ExecuteScalarAsync<decimal>(
            """
            SELECT COALESCE(SUM("Importe"), 0) FROM "MovimientosContables"
            WHERE "CuentaContableId" = @cuentaId AND "ProductoId" = @productoId
            """,
            new { cuentaId, productoId });
    }

    private async Task<decimal> ValorInventarioAsync(Guid productoId)
    {
        await using var conexion = _prueba.NuevaConexion();
        return await conexion.ExecuteScalarAsync<decimal>(
            """SELECT COALESCE(SUM("ImporteCosto"), 0) FROM "MovimientosValor" WHERE "ProductoId" = @productoId""", new { productoId });
    }

    private async Task<int> PendientesDelProductoAsync(Guid productoId)
    {
        await using var conexion = _prueba.NuevaConexion();
        return await conexion.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM "MovimientosValor"
            WHERE "ProductoId" = @productoId AND "ImporteCosto" <> "ImporteCostoPosteadoContabilidad"
            """,
            new { productoId });
    }

    private async Task<bool> ContabilizadoAsync(long movimientoValorId)
    {
        await using var conexion = _prueba.NuevaConexion();
        return await conexion.ExecuteScalarAsync<bool>(
            """SELECT "ImporteCosto" = "ImporteCostoPosteadoContabilidad" FROM "MovimientosValor" WHERE "Id" = @movimientoValorId""",
            new { movimientoValorId });
    }

    private async Task<int> RegistrosDelProductoAsync(Guid productoId)
    {
        await using var conexion = _prueba.NuevaConexion();
        return await conexion.ExecuteScalarAsync<int>(
            """SELECT COUNT(DISTINCT "RegistroContableId") FROM "MovimientosContables" WHERE "ProductoId" = @productoId""",
            new { productoId });
    }

    private async Task<(long Movimientos, long Registros)> FilasLibroContableAsync()
    {
        await using var conexion = _prueba.NuevaConexion();
        return await conexion.QuerySingleAsync<(long, long)>(
            """SELECT (SELECT COUNT(*) FROM "MovimientosContables"), (SELECT COUNT(*) FROM "RegistrosContables")""");
    }

    private async Task<List<LineaContable>> LineasAsync(Guid productoId)
    {
        await using var conexion = _prueba.NuevaConexion();
        return (await conexion.QueryAsync<LineaContable>(
            """
            SELECT "NumeroCuenta", "Importe", "FechaRegistro", "TipoOrigen", "TipoDocumento", "GrupoProductoId"
            FROM "MovimientosContables" WHERE "ProductoId" = @productoId ORDER BY "Id"
            """,
            new { productoId })).ToList();
    }

    private async Task AssertTodosLosRegistrosCuadranAsync()
    {
        await using var conexion = _prueba.NuevaConexion();
        var descuadrados = await conexion.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM (
                SELECT "RegistroContableId" FROM "MovimientosContables" GROUP BY "RegistroContableId" HAVING SUM("Importe") <> 0) x
            """);
        Assert.Equal(0, descuadrados);
    }

    private sealed class LineaContable
    {
        public string NumeroCuenta { get; init; } = string.Empty;
        public decimal Importe { get; init; }
        public DateOnly FechaRegistro { get; init; }
        public short TipoOrigen { get; init; }
        public short TipoDocumento { get; init; }
        public Guid? GrupoProductoId { get; init; }
    }
}
