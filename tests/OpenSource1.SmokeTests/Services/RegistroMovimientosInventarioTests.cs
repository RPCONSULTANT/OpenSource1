using System.Collections.Concurrent;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenSource1.Application.Data;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Services;

/// <summary>
/// <see cref="IRegistroMovimientosInventario"/> contra Postgres real (Task 3.4): costo promedio móvil por día, aplicaciones
/// FIFO, existencia derivada por almacén/fecha, validaciones sin escritura parcial y serialización por producto con
/// <c>pg_advisory_xact_lock</c>. REQUIERE DOCKER. Cada test siembra su propio producto/almacén.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RegistroMovimientosInventarioTests(PostgresTestFixture fixture)
    : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private static readonly DateOnly D1 = new(2026, 3, 1);
    private static readonly DateOnly D2 = new(2026, 3, 2);
    private static readonly DateOnly D3 = new(2026, 3, 3);
    private static readonly DateOnly D4 = new(2026, 3, 4);

    private readonly LibroInventarioPrueba _prueba = new(fixture);

    public Task InitializeAsync() => _prueba.InicializarAsync();

    public async Task DisposeAsync() => await _prueba.DisposeAsync();

    [Fact]
    public async Task Entrada_DiezADiez_DejaExistenciaValorYRestante()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();

        var registrado = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 10m, D1));

        Assert.Equal(10m, registrado.CantidadBase);
        Assert.Equal(100m, registrado.ImporteCosto);
        Assert.Equal(10m, await _prueba.ConsultarAsync(c => c.ExistenciaAsync(producto, almacen, null)));

        await using var conexion = _prueba.NuevaConexion();
        var mp = await conexion.QuerySingleAsync<(decimal Cantidad, decimal? Restante, decimal Factor, string CreatedBy, Guid? UsuarioId, DateTime CreatedAt)>(
            """SELECT "Cantidad", "CantidadRestante", "CantidadPorUnidadMedida", "CreatedBy", "UsuarioId", "CreatedAtUtc" FROM "MovimientosProducto" WHERE "Id" = @id""",
            new { id = registrado.MovimientoProductoId });
        Assert.Equal(10m, mp.Cantidad);
        Assert.Equal(10m, mp.Restante);
        Assert.Equal(1m, mp.Factor);
        Assert.Equal("system", mp.CreatedBy);
        Assert.Null(mp.UsuarioId);
        Assert.True(Math.Abs((DateTime.UtcNow - mp.CreatedAt.ToUniversalTime()).TotalMinutes) < 5);

        var mv = await conexion.QuerySingleAsync<(long? MovProd, decimal Cantidad, decimal Importe, decimal CostoUnidad, short TipoValor)>(
            """SELECT "MovimientoProductoId", "CantidadValorada", "ImporteCosto", "CostoPorUnidad", "TipoValor" FROM "MovimientosValor" WHERE "Id" = @id""",
            new { id = registrado.MovimientoValorId });
        Assert.Equal(registrado.MovimientoProductoId, mv.MovProd);
        Assert.Equal(10m, mv.Cantidad);
        Assert.Equal(100m, mv.Importe);
        Assert.Equal(10m, mv.CostoUnidad);
        Assert.Equal((short)TipoValor.CostoDirecto, mv.TipoValor);
    }

    [Fact]
    public async Task Salida_TrasDosEntradasADistintoCosto_UsaElPromedioYAplicaFifoContraLaPrimera()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        var e1 = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 10m, D1));
        var e2 = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 20m, D2));

        await using var conexion = _prueba.NuevaConexion();
        const string filaSql = """
            SELECT (to_jsonb(m) - 'CantidadRestante')::text AS "Resto", m."CantidadRestante" AS "Restante"
            FROM "MovimientosProducto" m WHERE m."Id" = @id
            """;
        var antes = await conexion.QuerySingleAsync<(string Resto, decimal Restante)>(filaSql, new { id = e1.MovimientoProductoId });

        var salida = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 5m, D3));

        Assert.Equal(5m, salida.CantidadBase);
        Assert.Equal(-75m, salida.ImporteCosto);

        var costoUnidad = await conexion.QuerySingleAsync<decimal>(
            """SELECT "CostoPorUnidad" FROM "MovimientosValor" WHERE "Id" = @id""", new { id = salida.MovimientoValorId });
        Assert.Equal(15m, costoUnidad);

        var aplicaciones = (await conexion.QueryAsync<(long Entrada, decimal Cantidad, DateOnly Fecha)>(
            """SELECT "MovimientoEntradaId", "Cantidad", "FechaRegistro" FROM "AplicacionesMovimientoProducto" WHERE "MovimientoSalidaId" = @id""",
            new { id = salida.MovimientoProductoId })).ToList();
        var aplicacion = Assert.Single(aplicaciones);
        Assert.Equal(e1.MovimientoProductoId, aplicacion.Entrada);
        Assert.Equal(5m, aplicacion.Cantidad);
        Assert.Equal(D3, aplicacion.Fecha);

        // La escritura real de CantidadRestante pasó por la rama permitida del trigger: bajó y NINGUNA otra columna cambió.
        var despues = await conexion.QuerySingleAsync<(string Resto, decimal Restante)>(filaSql, new { id = e1.MovimientoProductoId });
        Assert.Equal(10m, antes.Restante);
        Assert.Equal(5m, despues.Restante);
        Assert.Equal(antes.Resto, despues.Resto);

        var restanteE2 = await conexion.QuerySingleAsync<decimal>(
            """SELECT "CantidadRestante" FROM "MovimientosProducto" WHERE "Id" = @id""", new { id = e2.MovimientoProductoId });
        Assert.Equal(10m, restanteE2);

        var restanteSalida = await conexion.QuerySingleAsync<decimal?>(
            """SELECT "CantidadRestante" FROM "MovimientosProducto" WHERE "Id" = @id""", new { id = salida.MovimientoProductoId });
        Assert.Null(restanteSalida);

        Assert.False(await CostoAjustadoAsync(producto));
    }

    [Fact]
    public async Task Salida_QueCruzaDosEntradas_AplicaFifoEnOrdenDeFechaEId()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        // Registradas en orden inverso a su fecha: el orden FIFO es (FechaRegistro, Id), no el de inserción.
        var tardia = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 20m, D2));
        var temprana = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 10m, D1));

        var salida = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 15m, D3));

        await using var conexion = _prueba.NuevaConexion();
        var aplicaciones = (await conexion.QueryAsync<(long Entrada, decimal Cantidad)>(
            """SELECT "MovimientoEntradaId", "Cantidad" FROM "AplicacionesMovimientoProducto" WHERE "MovimientoSalidaId" = @id ORDER BY "Id" """,
            new { id = salida.MovimientoProductoId })).ToList();

        Assert.Equal(
            [(temprana.MovimientoProductoId, 10m), (tardia.MovimientoProductoId, 5m)],
            aplicaciones);
    }

    [Fact]
    public async Task EjemploDelSpecCorregido_CompraVentaCompraVenta_CuestaVeinteYDejaValorCero()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();

        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 10m, D1));
        var s1 = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 10m, D2));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 20m, D3));
        var s2 = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 10m, D4));

        Assert.Equal(-100m, s1.ImporteCosto);
        Assert.Equal(-200m, s2.ImporteCosto);
        Assert.Equal(0m, await _prueba.ConsultarAsync(c => c.ExistenciaAsync(producto, null, null)));

        await using var conexion = _prueba.NuevaConexion();
        var valorFinal = await conexion.QuerySingleAsync<decimal>(
            """SELECT SUM("ImporteCosto") FROM "MovimientosValor" WHERE "ProductoId" = @p""", new { p = producto });
        Assert.Equal(0m, valorFinal);
    }

    [Fact]
    public async Task UnidadAlternativa_SalidaEnCajas_CongelaFactorDoceYEntradaEnBaseFactorUno()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();

        var entrada = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 24m, 5m, D1));
        var salida = await _prueba.RegistrarOkAsync(
            LibroInventarioPrueba.Salida(producto, almacen, 1m, D2, unidadId: _prueba.UnidadCja));

        Assert.Equal(12m, salida.CantidadBase);
        Assert.Equal(-60m, salida.ImporteCosto);

        await using var conexion = _prueba.NuevaConexion();
        const string sql = """SELECT "Cantidad", "CantidadPorUnidadMedida", "UnidadMedidaId" FROM "MovimientosProducto" WHERE "Id" = @id""";
        var filaSalida = await conexion.QuerySingleAsync<(decimal Cantidad, decimal Factor, Guid Unidad)>(sql, new { id = salida.MovimientoProductoId });
        var filaEntrada = await conexion.QuerySingleAsync<(decimal Cantidad, decimal Factor, Guid Unidad)>(sql, new { id = entrada.MovimientoProductoId });

        Assert.Equal((-12m, 12m, _prueba.UnidadCja), filaSalida);
        Assert.Equal((24m, 1m, LibroInventarioPrueba.UnidadUnd), filaEntrada);
        Assert.Equal(12m, await _prueba.ConsultarAsync(c => c.ExistenciaAsync(producto, almacen, null)));
    }

    [Fact]
    public async Task Salida_MayorQueLaExistencia_FallaSinEscribirNingunaFila()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 5m, 10m, D1));

        await using var scope = _prueba.Provider.CreateAsyncScope();
        var sesion = scope.ServiceProvider.GetRequiredService<IDbSession>();
        var registro = scope.ServiceProvider.GetRequiredService<IRegistroMovimientosInventario>();
        await using var tx = await sesion.BeginTransactionAsync();

        var antes = await LibroInventarioPrueba.ContarFilasAsync(sesion.Connection, producto, sesion.CurrentTransaction);
        var resultado = await registro.RegistrarAsync(LibroInventarioPrueba.Salida(producto, almacen, 6m, D2));
        var despues = await LibroInventarioPrueba.ContarFilasAsync(sesion.Connection, producto, sesion.CurrentTransaction);
        await sesion.RollbackAsync();

        Assert.True(resultado.EsFallo);
        var error = Assert.Single(resultado.Errores);
        Assert.Equal("inventario.existencia_insuficiente", error.Codigo);
        Assert.Equal("Cantidad", error.Campo);
        Assert.Contains("5", error.Mensaje);
        Assert.Equal((1L, 1L, 0L), antes);
        Assert.Equal(antes, despues);
        Assert.Equal(antes, await _prueba.ContarFilasAsync(producto));
        Assert.Equal(5m, await RestanteTotalAsync(producto));
    }

    [Fact]
    public async Task ExistenciaPorAlmacen_SalidaEnUnAlmacen_YSalidaEnOtroSinStockFallaAunqueElTotalAlcance()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacenA = await _prueba.SembrarAlmacenAsync();
        var almacenB = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacenA, 10m, 10m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacenB, 5m, 10m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacenA, 3m, D2));

        var existencias = await _prueba.ConsultarAsync(c => c.ExistenciasPorAlmacenAsync(producto));
        Assert.Equal(2, existencias.Count);
        Assert.Equal(7m, Assert.Single(existencias, e => e.AlmacenId == almacenA).Existencia);
        Assert.Equal(5m, Assert.Single(existencias, e => e.AlmacenId == almacenB).Existencia);
        Assert.Equal(12m, await _prueba.ConsultarAsync(c => c.ExistenciaAsync(producto, null, null)));

        var enB = await _prueba.RegistrarAsync(LibroInventarioPrueba.Salida(producto, almacenB, 6m, D2));
        Assert.True(enB.EsFallo);
        Assert.Equal("inventario.existencia_insuficiente", enB.Errores[0].Codigo);
    }

    [Fact]
    public async Task Fecha_ExistenciaAntesDeLaEntradaEsCero_YSalidaFechadaAntesEsInsuficiente()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 10m, D2));

        Assert.Equal(0m, await _prueba.ConsultarAsync(c => c.ExistenciaAsync(producto, almacen, D1)));
        Assert.Equal(10m, await _prueba.ConsultarAsync(c => c.ExistenciaAsync(producto, almacen, D2)));

        var antes = await _prueba.RegistrarAsync(LibroInventarioPrueba.Salida(producto, almacen, 1m, D1));
        Assert.True(antes.EsFallo);
        Assert.Equal("inventario.existencia_insuficiente", antes.Errores[0].Codigo);
    }

    [Fact]
    public async Task Salida_RetroactivaSinRestanteAbiertoSuficiente_EsInsuficienteAunqueLaExistenciaALaFechaAlcance()
    {
        // Existencia a D2 = 10, pero una salida en D3 ya consumió 8 de la única entrada: solo quedan 2 abiertos.
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 10m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 8m, D3));

        var resultado = await _prueba.RegistrarAsync(LibroInventarioPrueba.Salida(producto, almacen, 5m, D2));

        Assert.True(resultado.EsFallo);
        Assert.Equal("inventario.existencia_insuficiente", resultado.Errores[0].Codigo);
    }

    public static TheoryData<string> CasosDeReferenciaInvalida => new()
    {
        "producto_bloqueado_todo", "almacen_bloqueado", "producto_borrado", "producto_inexistente", "almacen_inexistente",
    };

    [Theory]
    [MemberData(nameof(CasosDeReferenciaInvalida))]
    public async Task ReferenciasInvalidasOBloqueadas_FallanConSuCodigoYSinEscribir(string caso)
    {
        var producto = caso switch
        {
            "producto_bloqueado_todo" => await _prueba.SembrarProductoAsync(bloqueado: BloqueoProducto.Todo),
            "producto_borrado" => await _prueba.SembrarProductoAsync(borrado: true),
            "producto_inexistente" => Guid.NewGuid(),
            _ => await _prueba.SembrarProductoAsync(),
        };
        var almacen = caso switch
        {
            "almacen_bloqueado" => await _prueba.SembrarAlmacenAsync(bloqueado: true),
            "almacen_inexistente" => Guid.NewGuid(),
            _ => await _prueba.SembrarAlmacenAsync(),
        };
        var esperado = caso switch
        {
            "producto_bloqueado_todo" => ("inventario.producto_bloqueado", "ProductoId"),
            "almacen_bloqueado" => ("inventario.almacen_bloqueado", "AlmacenId"),
            "producto_borrado" or "producto_inexistente" => ("inventario.producto_invalido", "ProductoId"),
            _ => ("inventario.almacen_invalido", "AlmacenId"),
        };

        var resultado = await _prueba.RegistrarAsync(LibroInventarioPrueba.Entrada(producto, almacen, 1m, 1m, D1));

        Assert.True(resultado.EsFallo);
        var error = Assert.Single(resultado.Errores);
        Assert.Equal(esperado, (error.Codigo, error.Campo));
        Assert.DoesNotContain(".no_encontrado", error.Codigo);
        Assert.Equal((0L, 0L, 0L), await _prueba.ContarFilasAsync(producto));
    }

    [Fact]
    public async Task ProductoBloqueadoSoloParaVenta_PermiteMovimientos()
    {
        var producto = await _prueba.SembrarProductoAsync(bloqueado: BloqueoProducto.Venta);
        var almacen = await _prueba.SembrarAlmacenAsync();

        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 1m, 1m, D1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CantidadNoPositiva_FallaConCantidadInvalida(int cantidad)
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();

        var resultado = await _prueba.RegistrarAsync(LibroInventarioPrueba.Entrada(producto, almacen, cantidad, 1m, D1));

        Assert.True(resultado.EsFallo);
        Assert.Equal(("inventario.cantidad_invalida", "Cantidad"), (resultado.Errores[0].Codigo, resultado.Errores[0].Campo));
        Assert.Equal((0L, 0L, 0L), await _prueba.ContarFilasAsync(producto));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-0.01)]
    public async Task EntradaNoTransferencia_SinCostoONegativo_FallaConCostoRequerido(double? costo)
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();

        var resultado = await _prueba.RegistrarAsync(
            LibroInventarioPrueba.Entrada(producto, almacen, 1m, costo is null ? null : (decimal)costo.Value, D1));

        Assert.True(resultado.EsFallo);
        Assert.Equal(("inventario.costo_requerido", "CostoUnitario"), (resultado.Errores[0].Codigo, resultado.Errores[0].Campo));
        Assert.Equal((0L, 0L, 0L), await _prueba.ContarFilasAsync(producto));
    }

    [Fact]
    public async Task SinTransaccionActiva_FallaConSinTransaccion()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();

        await using var scope = _prueba.Provider.CreateAsyncScope();
        var registro = scope.ServiceProvider.GetRequiredService<IRegistroMovimientosInventario>();

        var resultado = await registro.RegistrarAsync(LibroInventarioPrueba.Entrada(producto, almacen, 1m, 1m, D1));

        Assert.True(resultado.EsFallo);
        Assert.Equal("inventario.sin_transaccion", resultado.Errores[0].Codigo);
        Assert.Equal((0L, 0L, 0L), await _prueba.ContarFilasAsync(producto));
    }

    [Fact]
    public async Task CostoDeReserva_SinCantidadValoradaPositiva_UsaCostoUnitarioDelProductoYMarcaAjustePendiente()
    {
        // Q <= 0 real: la única entrada es una transferencia del MISMO día, que la fórmula excluye (solo cuentan las
        // entradas no transferencia del día), así que no hay cantidad valorada previa y se usa Producto.CostoUnitario.
        var producto = await _prueba.SembrarProductoAsync(costoUnitario: 7m);
        var almacen = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(
            producto, almacen, 10m, 5m, D1, tipo: TipoMovimientoInventario.Transferencia));
        await EstablecerCostoAjustadoAsync(producto, true);

        Assert.Null(await _prueba.ConsultarAsync(c => c.CostoPromedioAsync(producto, D1)));

        var salida = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(
            producto, almacen, 4m, D1, tipo: TipoMovimientoInventario.Transferencia));

        Assert.Equal(-28m, salida.ImporteCosto);
        Assert.False(await CostoAjustadoAsync(producto));
    }

    [Fact]
    public async Task EntradaTransferenciaSinCosto_SeValoraAlPromedioVigente()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var origen = await _prueba.SembrarAlmacenAsync();
        var destino = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, origen, 10m, 10m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, origen, 10m, 20m, D1));

        var sale = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(
            producto, origen, 4m, D2, tipo: TipoMovimientoInventario.Transferencia));
        var entra = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(
            producto, destino, 4m, null, D2, tipo: TipoMovimientoInventario.Transferencia));

        Assert.Equal(-60m, sale.ImporteCosto);
        Assert.Equal(60m, entra.ImporteCosto);
    }

    [Fact]
    public async Task EntradaFechadaAntesDeLaUltimaSalida_MarcaCostoNoAjustado_YPosteriorNo()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 10m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 2m, D3));

        await EstablecerCostoAjustadoAsync(producto, true);
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 1m, 10m, D4));
        Assert.True(await CostoAjustadoAsync(producto));

        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 1m, 10m, D2));
        Assert.False(await CostoAjustadoAsync(producto));
    }

    [Fact]
    public async Task Concurrencia_DosSalidasDeSieteConExistenciaDiez_UnaGanaYLaOtraEsInsuficiente()
    {
        var producto = await _prueba.SembrarProductoAsync();
        var almacen = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 10m, D1));

        // Barrera: las dos transacciones están abiertas antes de que cualquiera llame a RegistrarAsync, y la ganadora
        // retiene su transacción un rato tras registrar. Sin el advisory lock, la segunda leería la existencia (10)
        // antes del commit de la primera y ambas pasarían la validación.
        var listas = 0;
        var barrera = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resultados = new ConcurrentBag<Result<MovimientoRegistrado>>();

        async Task SalirAsync()
        {
            await using var scope = _prueba.Provider.CreateAsyncScope();
            var sesion = scope.ServiceProvider.GetRequiredService<IDbSession>();
            var registro = scope.ServiceProvider.GetRequiredService<IRegistroMovimientosInventario>();
            await using var tx = await sesion.BeginTransactionAsync();

            if (Interlocked.Increment(ref listas) == 2)
            {
                barrera.SetResult();
            }

            await barrera.Task;
            var resultado = await registro.RegistrarAsync(LibroInventarioPrueba.Salida(producto, almacen, 7m, D2));
            await Task.Delay(300);
            if (resultado.EsExito)
            {
                await sesion.CommitAsync();
            }
            else
            {
                await sesion.RollbackAsync();
            }

            resultados.Add(resultado);
        }

        await Task.WhenAll(Task.Run(SalirAsync), Task.Run(SalirAsync));

        Assert.Equal(2, resultados.Count);
        Assert.Single(resultados, r => r.EsExito);
        var perdedora = Assert.Single(resultados, r => r.EsFallo);
        Assert.Equal("inventario.existencia_insuficiente", perdedora.Errores[0].Codigo);
        Assert.Equal(3m, await _prueba.ConsultarAsync(c => c.ExistenciaAsync(producto, almacen, null)));
        Assert.Equal(3m, await RestanteTotalAsync(producto));
    }

    private async Task<bool> CostoAjustadoAsync(Guid productoId)
    {
        await using var contexto = _prueba.NuevoContexto();
        return await contexto.Productos.IgnoreQueryFilters().Where(p => p.Id == productoId)
            .Select(p => p.CostoAjustado).SingleAsync();
    }

    private async Task EstablecerCostoAjustadoAsync(Guid productoId, bool valor)
    {
        await using var conexion = _prueba.NuevaConexion();
        await conexion.ExecuteAsync("""UPDATE "Productos" SET "CostoAjustado" = @v WHERE "Id" = @p""", new { v = valor, p = productoId });
    }

    private async Task<decimal> RestanteTotalAsync(Guid productoId)
    {
        await using var conexion = _prueba.NuevaConexion();
        return await conexion.QuerySingleAsync<decimal>(
            """SELECT COALESCE(SUM("CantidadRestante"), 0) FROM "MovimientosProducto" WHERE "ProductoId" = @p""", new { p = productoId });
    }
}
