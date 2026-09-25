using System.Collections.Concurrent;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.Almacenes.Commands;
using OpenSource1.Application.Features.Almacenes.Handlers;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Commands;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Handlers;
using OpenSource1.Application.Features.DiariosInventario.Registros.Commands;
using OpenSource1.Application.Features.DiariosInventario.Registros.Handlers;
using OpenSource1.Application.Features.Productos.Commands;
using OpenSource1.Application.Features.Productos.Handlers;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Inventario;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Features.DiariosInventario;

/// <summary>
/// Registro (posteo) de lotes de diario contra Postgres real (Task 4.3): atomicidad (un fallo no deja NINGUNA fila, ni
/// consume el número de la serie), orden de registro, reclasificación con suma de costo cero, factor congelado, doble
/// registro y concurrencia, y los bloqueos pendientes de la Fase 3 (borrar almacén/producto y cambiar la unidad base en
/// paralelo con un registro). REQUIERE DOCKER. Todos los lotes usan la serie DIARIO-INV (la de las plantillas), así que
/// los números se comprueban RELATIVOS al último usado (los tests de la clase comparten base de datos).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PostearLoteDiarioTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private static readonly DateOnly D1 = new(2026, 9, 1);
    private static readonly DateOnly D2 = new(2026, 9, 2);

    private readonly LibroInventarioPrueba _prueba = new(fixture);

    public Task InitializeAsync() => _prueba.InicializarAsync();

    public async Task DisposeAsync() => await _prueba.DisposeAsync();

    [Fact]
    public async Task LoteArticulo_RegistraMovimientos_RegistroDiario_BorraLineas_YMarcaCostoNoAjustado()
    {
        var p1 = await _prueba.SembrarProductoAsync();
        var p2 = await _prueba.SembrarProductoAsync();
        var alm1 = await _prueba.SembrarAlmacenAsync();
        var alm2 = await _prueba.SembrarAlmacenAsync();
        var lote = await CrearLoteAsync(PlantillaDiarioIds.Articulo);
        await AgregarLineaAsync(lote, TipoMovimientoInventario.AjustePositivo, p1, alm1, 10m, 5m, D1);
        await AgregarLineaAsync(lote, TipoMovimientoInventario.AjusteNegativo, p1, alm1, 3m, null, D1);
        await AgregarLineaAsync(lote, TipoMovimientoInventario.AjustePositivo, p2, alm2, 2m, 7m, D1);
        Assert.True(await CostoAjustadoAsync(p1));
        var ultimo = await UltimoNumeroAsync();

        var registro = await PostearOkAsync(lote);

        Assert.Equal(Siguiente(ultimo), registro.NumeroRegistro);
        Assert.Equal(3, registro.Movimientos);
        Assert.Equal(7m, await ExistenciaAsync(p1, alm1));
        Assert.Equal(2m, await ExistenciaAsync(p2, alm2));

        var movimientos = await MovimientosAsync(registro.NumeroRegistro);
        Assert.Equal(3, movimientos.Count);
        Assert.All(movimientos, m =>
        {
            Assert.Equal(TipoDocumentoInventario.RegistroDiario, m.TipoDocumento);
            Assert.Equal(TipoOrigenMovimiento.Diario, m.TipoOrigen);
            Assert.Equal(registro.NumeroRegistro, m.NumeroDocumento); // la línea no traía NumeroDocumento
        });
        Assert.Equal([10000, 20000, 30000], movimientos.Select(m => m.NumeroLineaDocumento).Order());
        Assert.Equal(-3m, Assert.Single(movimientos, m => m.NumeroLineaDocumento == 20000).Cantidad);

        var fila = Assert.Single(await RegistrosDelLoteAsync(lote));
        Assert.Equal(registro.NumeroRegistro, fila.NumeroRegistro);
        Assert.Equal(3, fila.Lineas);
        Assert.Equal(movimientos.Min(m => m.Id), fila.DesdeMovimientoProducto);
        Assert.Equal(movimientos.Max(m => m.Id), fila.HastaMovimientoProducto);
        Assert.Equal((fila.DesdeMovimientoProducto, fila.HastaMovimientoProducto), (registro.DesdeMovimientoProducto, registro.HastaMovimientoProducto));
        Assert.Equal("system", fila.CreadoPor);

        Assert.Equal(0, await LineasVivasAsync(lote));
        Assert.Equal(3, await LineasBorradasAsync(lote));
        Assert.False(await CostoAjustadoAsync(p1));
    }

    [Fact]
    public async Task ReviewFocus1_TerceraDeCincoLineasSaleDeMas_NoEscribeNadaNiConsumeElNumero()
    {
        var p1 = await _prueba.SembrarProductoAsync();
        var p2 = await _prueba.SembrarProductoAsync();
        var alm = await _prueba.SembrarAlmacenAsync();
        var lote = await CrearLoteAsync(PlantillaDiarioIds.Articulo);
        await AgregarLineaAsync(lote, TipoMovimientoInventario.AjustePositivo, p1, alm, 10m, 1m, D1);
        await AgregarLineaAsync(lote, TipoMovimientoInventario.AjustePositivo, p2, alm, 5m, 1m, D1);
        var mala = await AgregarLineaAsync(lote, TipoMovimientoInventario.AjusteNegativo, p1, alm, 50m, null, D1);
        await AgregarLineaAsync(lote, TipoMovimientoInventario.AjustePositivo, p1, alm, 1m, 1m, D1);
        await AgregarLineaAsync(lote, TipoMovimientoInventario.AjusteNegativo, p2, alm, 1m, null, D1);
        var ultimo = await UltimoNumeroAsync();

        var resultado = await PostearAsync(lote);

        Assert.True(resultado.EsFallo);
        var error = Assert.Single(resultado.Errores);
        Assert.Equal("inventario.existencia_insuficiente", error.Codigo);
        Assert.Equal("Lineas[30000].Cantidad", error.Campo);
        Assert.Contains("30000", error.Mensaje);
        Assert.Equal((0L, 0L, 0L), await _prueba.ContarFilasAsync(p1));
        Assert.Equal((0L, 0L, 0L), await _prueba.ContarFilasAsync(p2));
        Assert.Empty(await RegistrosDelLoteAsync(lote));
        Assert.Equal(5, await LineasVivasAsync(lote));
        Assert.Equal(ultimo, await UltimoNumeroAsync());

        // Corregido el lote, el siguiente registro correcto recibe el número que el fallo NO consumió.
        await using (var scope = _prueba.Provider.CreateAsyncScope())
        {
            var borrar = ActivatorUtilities.CreateInstance<DeleteLineaDiarioCommandHandler>(scope.ServiceProvider);
            Assert.True((await borrar.Handle(new DeleteLineaDiarioCommand(mala.Id), default)).EsExito);
        }

        var registro = await PostearOkAsync(lote);
        Assert.Equal(Siguiente(ultimo), registro.NumeroRegistro);
        Assert.Equal(11m, await ExistenciaAsync(p1, alm));
        Assert.Equal(4m, await ExistenciaAsync(p2, alm));
    }

    [Fact]
    public async Task Prevalidacion_DosLineasInvalidas_DevuelveLosDosErroresSinEscribir()
    {
        var p1 = await _prueba.SembrarProductoAsync();
        var p2 = await _prueba.SembrarProductoAsync();
        var alm1 = await _prueba.SembrarAlmacenAsync();
        var alm2 = await _prueba.SembrarAlmacenAsync();
        var lote = await CrearLoteAsync(PlantillaDiarioIds.Articulo);
        await AgregarLineaAsync(lote, TipoMovimientoInventario.AjustePositivo, p1, alm1, 1m, 1m, D1);
        await AgregarLineaAsync(lote, TipoMovimientoInventario.AjustePositivo, p2, alm2, 1m, 1m, D1);
        await EjecutarSqlAsync("""UPDATE "Productos" SET "Bloqueado" = 2 WHERE "Id" = @Id""", new { Id = p1 });
        await EjecutarSqlAsync("""UPDATE "Almacenes" SET "Bloqueado" = true WHERE "Id" = @Id""", new { Id = alm2 });
        var ultimo = await UltimoNumeroAsync();

        var resultado = await PostearAsync(lote);

        Assert.True(resultado.EsFallo);
        Assert.Equal(
            [("diario.producto_invalido", "Lineas[10000].ProductoId"), ("diario.almacen_invalido", "Lineas[20000].AlmacenId")],
            resultado.Errores.Select(e => (e.Codigo, e.Campo))); // en orden de NumeroLinea
        Assert.Equal((0L, 0L, 0L), await _prueba.ContarFilasAsync(p2));
        Assert.Equal(2, await LineasVivasAsync(lote));
        Assert.Equal(ultimo, await UltimoNumeroAsync());
    }

    [Fact]
    public async Task ReviewFocus5_Reclasificacion_DosTransferenciasSumaCostoCeroYExistenciaTotalIgual()
    {
        // Promedio 8/6 = 1.3333...: la salida de 4 vale -5.3333 y el costo de su gemela, 5.3333 / 4 = 1.333325 (más de 4
        // decimales). La entrada debe valer EXACTAMENTE +5.3333.
        var p = await _prueba.SembrarProductoAsync();
        var alm1 = await _prueba.SembrarAlmacenAsync();
        var alm2 = await _prueba.SembrarAlmacenAsync();
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(p, alm1, 2m, 1m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(p, alm1, 2m, 1m, D1));
        await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(p, alm1, 2m, 2m, D1));
        var lote = await CrearLoteAsync(PlantillaDiarioIds.Reclasificacion);
        await AgregarLineaAsync(lote, TipoMovimientoInventario.Transferencia, p, alm1, 4m, null, D2, destino: alm2);

        var registro = await PostearOkAsync(lote);

        Assert.Equal(2, registro.Movimientos);
        var movimientos = await MovimientosAsync(registro.NumeroRegistro);
        Assert.Equal(2, movimientos.Count);
        Assert.All(movimientos, m =>
        {
            Assert.Equal(TipoMovimientoInventario.Transferencia, m.TipoMovimiento);
            Assert.Equal(10000, m.NumeroLineaDocumento);
        });
        Assert.Equal(-4m, Assert.Single(movimientos, m => m.AlmacenId == alm1).Cantidad);
        Assert.Equal(4m, Assert.Single(movimientos, m => m.AlmacenId == alm2).Cantidad);

        var importes = await ImportesValorAsync(registro.NumeroRegistro);
        Assert.Equal([-5.3333m, 5.3333m], importes.Order());
        Assert.Equal(0m, importes.Sum());

        Assert.Equal(6m, await ExistenciaAsync(p, null));
        Assert.Equal(2m, await ExistenciaAsync(p, alm1));
        Assert.Equal(4m, await ExistenciaAsync(p, alm2));
    }

    [Fact]
    public async Task ReviewFocus5_Reclasificacion_OrigenIgualADestino_SeRechazaAlGuardar()
    {
        var p = await _prueba.SembrarProductoAsync();
        var alm = await _prueba.SembrarAlmacenAsync();
        var lote = await CrearLoteAsync(PlantillaDiarioIds.Reclasificacion);

        var resultado = await CrearLineaAsync(lote, TipoMovimientoInventario.Transferencia, p, alm, 1m, null, D1, destino: alm);

        Assert.True(resultado.EsFallo);
        Assert.Equal(("diario.almacen_destino_invalido", "AlmacenDestinoId"), (resultado.Errores[0].Codigo, resultado.Errores[0].Campo));
    }

    [Fact]
    public async Task SalidaYEntradaDelMismoDia_ConLaSalidaEnUnaLineaAnterior_SeRegistra()
    {
        var p = await _prueba.SembrarProductoAsync();
        var alm = await _prueba.SembrarAlmacenAsync();
        var lote = await CrearLoteAsync(PlantillaDiarioIds.Articulo);
        await AgregarLineaAsync(lote, TipoMovimientoInventario.AjusteNegativo, p, alm, 3m, null, D1);
        await AgregarLineaAsync(lote, TipoMovimientoInventario.AjustePositivo, p, alm, 10m, 2m, D1);

        var registro = await PostearOkAsync(lote);

        Assert.Equal(2, registro.Movimientos);
        Assert.Equal(7m, await ExistenciaAsync(p, alm));
    }

    [Fact]
    public async Task FactorCambiadoEntreGuardarYRegistrar_FallaConFactorCambiadoSinEscribir()
    {
        var p = await _prueba.SembrarProductoAsync();
        var alm = await _prueba.SembrarAlmacenAsync();
        var lote = await CrearLoteAsync(PlantillaDiarioIds.Articulo);
        var linea = await AgregarLineaAsync(lote, TipoMovimientoInventario.AjustePositivo, p, alm, 1m, 1m, D1, unidad: _prueba.UnidadCja);
        Assert.Equal(12m, linea.CantidadPorUnidadMedida);
        await EjecutarSqlAsync(
            """UPDATE "UnidadesMedidaProducto" SET "CantidadPorUnidadMedida" = 10 WHERE "ProductoId" = @P AND "UnidadMedidaId" = @U""",
            new { P = p, U = _prueba.UnidadCja });
        var ultimo = await UltimoNumeroAsync();

        var resultado = await PostearAsync(lote);

        Assert.True(resultado.EsFallo);
        var error = Assert.Single(resultado.Errores);
        Assert.Equal(("diario.factor_cambiado", "Lineas[10000].UnidadMedidaId"), (error.Codigo, error.Campo));
        Assert.Equal((0L, 0L, 0L), await _prueba.ContarFilasAsync(p));
        Assert.Equal(1, await LineasVivasAsync(lote));
        Assert.Equal(ultimo, await UltimoNumeroAsync());
    }

    [Fact]
    public async Task LoteInexistente_NoEncontrado_YLoteBloqueado_LoteBloqueado()
    {
        var inexistente = await PostearAsync(Guid.NewGuid());
        Assert.Equal("diario.no_encontrado", inexistente.Errores[0].Codigo);

        var p = await _prueba.SembrarProductoAsync();
        var alm = await _prueba.SembrarAlmacenAsync();
        var lote = await CrearLoteAsync(PlantillaDiarioIds.Articulo);
        await AgregarLineaAsync(lote, TipoMovimientoInventario.AjustePositivo, p, alm, 1m, 1m, D1);
        await EjecutarSqlAsync("""UPDATE "LotesDiario" SET "Bloqueado" = true WHERE "Id" = @Id""", new { Id = lote });

        var bloqueado = await PostearAsync(lote);

        Assert.Equal("diario.lote_bloqueado", bloqueado.Errores[0].Codigo);
        Assert.Equal((0L, 0L, 0L), await _prueba.ContarFilasAsync(p));
    }

    [Fact]
    public async Task ReviewFocus3_RegistrarDosVecesSeguidas_LaSegundaEsLoteVacioYNoDuplica()
    {
        var p = await _prueba.SembrarProductoAsync();
        var alm = await _prueba.SembrarAlmacenAsync();
        var lote = await CrearLoteAsync(PlantillaDiarioIds.Articulo);
        await AgregarLineaAsync(lote, TipoMovimientoInventario.AjustePositivo, p, alm, 1m, 1m, D1);
        await PostearOkAsync(lote);
        var ultimo = await UltimoNumeroAsync();

        var segunda = await PostearAsync(lote);

        Assert.Equal("diario.lote_vacio", Assert.Single(segunda.Errores).Codigo);
        Assert.Equal((1L, 1L, 0L), await _prueba.ContarFilasAsync(p));
        Assert.Single(await RegistrosDelLoteAsync(lote));
        Assert.Equal(ultimo, await UltimoNumeroAsync());
    }

    [Fact]
    public async Task ReviewFocus3_RegistrarElMismoLoteEnParalelo_UnExitoYUnLoteVacio_SinDuplicados()
    {
        // Varias rondas para que ambas peticiones lleguen de verdad a leer las líneas a la vez al menos una vez.
        for (var ronda = 0; ronda < 3; ronda++)
        {
            var p = await _prueba.SembrarProductoAsync();
            var alm = await _prueba.SembrarAlmacenAsync();
            var lote = await CrearLoteAsync(PlantillaDiarioIds.Articulo);
            await AgregarLineaAsync(lote, TipoMovimientoInventario.AjustePositivo, p, alm, 5m, 1m, D1);
            await AgregarLineaAsync(lote, TipoMovimientoInventario.AjustePositivo, p, alm, 3m, 1m, D1);

            var resultados = await EnParaleloAsync(() => PostearAsync(lote), () => PostearAsync(lote));

            Assert.Single(resultados, r => r.Resultado?.EsExito == true);
            var perdedora = Assert.Single(resultados, r => r.Resultado?.EsExito != true);
            Assert.Null(perdedora.Excepcion);
            Assert.Equal("diario.lote_vacio", Assert.Single(perdedora.Resultado!.Errores).Codigo);
            Assert.Equal((2L, 2L, 0L), await _prueba.ContarFilasAsync(p));
            Assert.Equal(8m, await ExistenciaAsync(p, alm));
            Assert.Single(await RegistrosDelLoteAsync(lote));
        }
    }

    [Fact]
    public async Task ReviewFocus2_DosLotesConLosMismosProductosEnOrdenInverso_AmbosTerminan()
    {
        for (var ronda = 0; ronda < 3; ronda++)
        {
            var p1 = await _prueba.SembrarProductoAsync();
            var p2 = await _prueba.SembrarProductoAsync();
            var alm = await _prueba.SembrarAlmacenAsync();
            var loteA = await CrearLoteAsync(PlantillaDiarioIds.Articulo);
            await AgregarLineaAsync(loteA, TipoMovimientoInventario.AjustePositivo, p1, alm, 1m, 1m, D1);
            await AgregarLineaAsync(loteA, TipoMovimientoInventario.AjustePositivo, p2, alm, 2m, 1m, D1);
            var loteB = await CrearLoteAsync(PlantillaDiarioIds.Articulo);
            await AgregarLineaAsync(loteB, TipoMovimientoInventario.AjustePositivo, p2, alm, 3m, 1m, D1);
            await AgregarLineaAsync(loteB, TipoMovimientoInventario.AjustePositivo, p1, alm, 4m, 1m, D1);

            var resultados = await EnParaleloAsync(() => PostearAsync(loteA), () => PostearAsync(loteB));

            Assert.All(resultados, r =>
            {
                Assert.Null(r.Excepcion);
                Assert.True(r.Resultado!.EsExito);
            });
            Assert.NotEqual(resultados[0].Resultado!.Valor.NumeroRegistro, resultados[1].Resultado!.Valor.NumeroRegistro);
            Assert.Equal(5m, await ExistenciaAsync(p1, alm));
            Assert.Equal(5m, await ExistenciaAsync(p2, alm));
        }
    }

    [Fact]
    public async Task ReviewFocus4_ModificarUnaLineaTrasRegistrarElLote_NoEncontradaYNoReaparece()
    {
        var p = await _prueba.SembrarProductoAsync();
        var alm = await _prueba.SembrarAlmacenAsync();
        var lote = await CrearLoteAsync(PlantillaDiarioIds.Articulo);
        var linea = await AgregarLineaAsync(lote, TipoMovimientoInventario.AjustePositivo, p, alm, 1m, 1m, D1);
        await PostearOkAsync(lote);

        await using var scope = _prueba.Provider.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<UpdateLineaDiarioCommandHandler>(scope.ServiceProvider);
        var resultado = await handler.Handle(new UpdateLineaDiarioCommand(
            linea.Id, D1, D1, null, TipoMovimientoInventario.AjustePositivo, p, alm, null, LibroInventarioPrueba.UnidadUnd,
            9m, 1m, null, linea.Xmin), default);

        Assert.True(resultado.EsFallo);
        Assert.Equal("diario_linea.no_encontrado", resultado.Errores[0].Codigo);
        Assert.Equal(0, await LineasVivasAsync(lote));
        Assert.Equal((1L, 1L, 0L), await _prueba.ContarFilasAsync(p));
    }

    [Fact]
    public async Task RegistroDiario_EsAppendOnly()
    {
        var p = await _prueba.SembrarProductoAsync();
        var alm = await _prueba.SembrarAlmacenAsync();
        var lote = await CrearLoteAsync(PlantillaDiarioIds.Articulo);
        await AgregarLineaAsync(lote, TipoMovimientoInventario.AjustePositivo, p, alm, 1m, 1m, D1);
        var registro = await PostearOkAsync(lote);

        await using var conexion = _prueba.NuevaConexion();
        var update = await Assert.ThrowsAsync<PostgresException>(() => conexion.ExecuteAsync(
            """UPDATE "RegistrosDiario" SET "Lineas" = 99 WHERE "NumeroRegistro" = @N""", new { N = registro.NumeroRegistro }));
        Assert.Equal("P0001", update.SqlState);
        var delete = await Assert.ThrowsAsync<PostgresException>(() => conexion.ExecuteAsync(
            """DELETE FROM "RegistrosDiario" WHERE "NumeroRegistro" = @N""", new { N = registro.NumeroRegistro }));
        Assert.Equal("P0001", delete.SqlState);
        var truncate = await Assert.ThrowsAsync<PostgresException>(() => conexion.ExecuteAsync("""TRUNCATE "RegistrosDiario" """));
        Assert.Equal("P0001", truncate.SqlState);
    }

    // ----- Bloqueos pendientes de la Fase 3 -----

    [Fact]
    public async Task BorrarAlmacenEnParaleloConUnRegistro_ElBorradoEsperaYDa409_NuncaMovimientoEnAlmacenBorrado()
    {
        var p = await _prueba.SembrarProductoAsync();
        var alm = await _prueba.SembrarAlmacenAsync();

        var borrado = await RegistrarReteniendoTransaccionAsync(
            LibroInventarioPrueba.Entrada(p, alm, 1m, 1m, D1),
            async sp => await ActivatorUtilities.CreateInstance<DeleteAlmacenCommandHandler>(sp)
                .Handle(new DeleteAlmacenCommand(alm), default));

        Assert.False(borrado.CompletoMientrasRegistroAbierto, "El borrado del almacén no esperó al registro en curso.");
        Assert.True(borrado.Resultado.EsFallo);
        Assert.Equal("almacen.conflicto", borrado.Resultado.Errores[0].Codigo);
        Assert.False(await AlmacenBorradoAsync(alm));
        Assert.Equal((1L, 1L, 0L), await _prueba.ContarFilasAsync(p));
    }

    [Fact]
    public async Task AlmacenBorradoAntesDeRegistrar_ElRegistroFallaConAlmacenInvalidoDeLaLinea()
    {
        var p = await _prueba.SembrarProductoAsync();
        var alm = await _prueba.SembrarAlmacenAsync();
        var lote = await CrearLoteAsync(PlantillaDiarioIds.Articulo);
        await AgregarLineaAsync(lote, TipoMovimientoInventario.AjustePositivo, p, alm, 1m, 1m, D1);
        await using (var scope = _prueba.Provider.CreateAsyncScope())
        {
            var borrar = ActivatorUtilities.CreateInstance<DeleteAlmacenCommandHandler>(scope.ServiceProvider);
            Assert.True((await borrar.Handle(new DeleteAlmacenCommand(alm), default)).EsExito);
        }

        var resultado = await PostearAsync(lote);

        Assert.Equal(("diario.almacen_invalido", "Lineas[10000].AlmacenId"), (resultado.Errores[0].Codigo, resultado.Errores[0].Campo));
        Assert.Equal((0L, 0L, 0L), await _prueba.ContarFilasAsync(p));
    }

    [Fact]
    public async Task BorrarProductoEnParaleloConUnRegistro_ElBorradoEsperaYDa409_NuncaMovimientoDeProductoBorrado()
    {
        var p = await _prueba.SembrarProductoAsync();
        var alm = await _prueba.SembrarAlmacenAsync();

        var borrado = await RegistrarReteniendoTransaccionAsync(
            LibroInventarioPrueba.Entrada(p, alm, 1m, 1m, D1),
            async sp => await ActivatorUtilities.CreateInstance<DeleteProductoCommandHandler>(sp)
                .Handle(new DeleteProductoCommand(p), default));

        Assert.False(borrado.CompletoMientrasRegistroAbierto, "El borrado del producto no esperó al registro en curso.");
        Assert.True(borrado.Resultado.EsFallo);
        Assert.Equal("producto.conflicto", borrado.Resultado.Errores[0].Codigo);
        Assert.False(await ProductoBorradoAsync(p));
        Assert.Equal((1L, 1L, 0L), await _prueba.ContarFilasAsync(p));
    }

    [Fact]
    public async Task CambiarUnidadBaseEnParaleloConUnRegistro_ElCambioEsperaYDa409()
    {
        var p = await _prueba.SembrarProductoAsync();
        var alm = await _prueba.SembrarAlmacenAsync();
        var codigo = await ConsultarEscalarAsync<string>("""SELECT "Codigo" FROM "Productos" WHERE "Id" = @Id""", new { Id = p });

        var cambio = await RegistrarReteniendoTransaccionAsync(
            LibroInventarioPrueba.Entrada(p, alm, 1m, 1m, D1),
            async sp => await ActivatorUtilities.CreateInstance<UpdateProductoCommandHandler>(sp)
                .Handle(new UpdateProductoCommand(p, codigo, "Producto renombrado", null, null, _prueba.UnidadCja, null, null, null), default));

        Assert.False(cambio.CompletoMientrasRegistroAbierto, "El cambio de unidad base no esperó al registro en curso.");
        Assert.True(cambio.Resultado.EsFallo);
        Assert.Equal(("producto.conflicto", "UnidadMedidaBaseId"), (cambio.Resultado.Errores[0].Codigo, cambio.Resultado.Errores[0].Campo));
        Assert.Equal(
            LibroInventarioPrueba.UnidadUnd,
            await ConsultarEscalarAsync<Guid>("""SELECT "UnidadMedidaBaseId" FROM "Productos" WHERE "Id" = @Id""", new { Id = p }));
    }

    // ----- Utilidades -----

    /// <summary>
    /// Barrera: abre una transacción, registra <paramref name="solicitud"/> (toma los bloqueos del producto y del almacén),
    /// y con la transacción AÚN abierta lanza <paramref name="accion"/> en otro scope; espera 700 ms, confirma el registro
    /// y devuelve el resultado de la acción y si terminó antes del commit (con los bloqueos, nunca debería).
    /// </summary>
    private async Task<(TResultado Resultado, bool CompletoMientrasRegistroAbierto)> RegistrarReteniendoTransaccionAsync<TResultado>(
        MovimientoInventarioSolicitud solicitud, Func<IServiceProvider, Task<TResultado>> accion)
    {
        await using var scopeRegistro = _prueba.Provider.CreateAsyncScope();
        var sesion = scopeRegistro.ServiceProvider.GetRequiredService<IDbSession>();
        var registro = scopeRegistro.ServiceProvider.GetRequiredService<IRegistroMovimientosInventario>();
        await using var tx = await sesion.BeginTransactionAsync();
        var registrado = await registro.RegistrarAsync(solicitud);
        Assert.True(registrado.EsExito, registrado.EsFallo ? registrado.Errores[0].Codigo : string.Empty);

        var tarea = Task.Run(async () =>
        {
            await using var scope = _prueba.Provider.CreateAsyncScope();
            return await accion(scope.ServiceProvider);
        });

        var completo = await Task.WhenAny(tarea, Task.Delay(700)) == tarea;
        await sesion.CommitAsync();

        return (await tarea.WaitAsync(TimeSpan.FromSeconds(30)), completo);
    }

    private static async Task<List<(Result<ResultadoRegistroLote>? Resultado, Exception? Excepcion)>> EnParaleloAsync(
        params Func<Task<Result<ResultadoRegistroLote>>>[] acciones)
    {
        var barrera = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listas = 0;
        var resultados = new ConcurrentBag<(Result<ResultadoRegistroLote>?, Exception?)>();

        async Task EjecutarAsync(Func<Task<Result<ResultadoRegistroLote>>> accion)
        {
            if (Interlocked.Increment(ref listas) == acciones.Length)
            {
                barrera.SetResult();
            }

            await barrera.Task;
            try
            {
                resultados.Add((await accion(), null));
            }
            catch (Exception ex)
            {
                resultados.Add((null, ex));
            }
        }

        await Task.WhenAll(acciones.Select(a => Task.Run(() => EjecutarAsync(a)))).WaitAsync(TimeSpan.FromSeconds(60));
        return [.. resultados];
    }

    private async Task<Guid> CrearLoteAsync(Guid plantillaId)
    {
        await using var contexto = _prueba.NuevoContexto();
        var lote = new LoteDiario
        {
            PlantillaDiarioId = plantillaId,
            Codigo = $"L{Guid.NewGuid():N}"[..15].ToUpperInvariant(),
            Nombre = "Lote de prueba del registro",
            CreatedBy = "test",
        };
        contexto.LotesDiario.Add(lote);
        await contexto.SaveChangesAsync();
        return lote.Id;
    }

    private async Task<Result<LineaDiarioResponse>> CrearLineaAsync(
        Guid loteId, TipoMovimientoInventario tipo, Guid productoId, Guid almacenId, decimal cantidad, decimal? costo,
        DateOnly fecha, Guid? destino = null, Guid? unidad = null)
    {
        await using var scope = _prueba.Provider.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<CreateLineaDiarioCommandHandler>(scope.ServiceProvider);
        return await handler.Handle(new CreateLineaDiarioCommand(
            loteId, fecha, fecha, null, tipo, productoId, almacenId, destino, unidad ?? LibroInventarioPrueba.UnidadUnd,
            cantidad, costo, null), default);
    }

    private async Task<LineaDiarioResponse> AgregarLineaAsync(
        Guid loteId, TipoMovimientoInventario tipo, Guid productoId, Guid almacenId, decimal cantidad, decimal? costo,
        DateOnly fecha, Guid? destino = null, Guid? unidad = null)
    {
        var resultado = await CrearLineaAsync(loteId, tipo, productoId, almacenId, cantidad, costo, fecha, destino, unidad);
        Assert.True(resultado.EsExito, resultado.EsFallo ? $"{resultado.Errores[0].Codigo}: {resultado.Errores[0].Mensaje}" : string.Empty);
        return resultado.Valor;
    }

    private async Task<Result<ResultadoRegistroLote>> PostearAsync(Guid loteId)
    {
        await using var scope = _prueba.Provider.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<PostearLoteDiarioCommandHandler>(scope.ServiceProvider);
        return await handler.Handle(new PostearLoteDiarioCommand(loteId), default);
    }

    private async Task<ResultadoRegistroLote> PostearOkAsync(Guid loteId)
    {
        var resultado = await PostearAsync(loteId);
        Assert.True(resultado.EsExito, resultado.EsFallo
            ? string.Join("; ", resultado.Errores.Select(e => $"{e.Codigo} [{e.Campo}]: {e.Mensaje}"))
            : string.Empty);
        return resultado.Valor;
    }

    private Task<decimal> ExistenciaAsync(Guid productoId, Guid? almacenId) =>
        _prueba.ConsultarAsync(c => c.ExistenciaAsync(productoId, almacenId, null));

    private Task<string> UltimoNumeroAsync() => ConsultarEscalarAsync<string>(
        """SELECT "UltimoNumeroUsado" FROM "LineasSerie" WHERE "Id" = @Id""", new { Id = SerieDiarioInventarioIds.LineaSerieId });

    private static string Siguiente(string ultimo) => (long.Parse(ultimo) + 1).ToString().PadLeft(6, '0');

    private async Task<List<MovimientoFila>> MovimientosAsync(string numeroRegistro)
    {
        await using var conexion = _prueba.NuevaConexion();
        return [.. await conexion.QueryAsync<MovimientoFila>(
            """
            SELECT "Id", "ProductoId", "AlmacenId", "TipoMovimiento", "TipoDocumento", "NumeroDocumento",
                   "NumeroLineaDocumento", "Cantidad", "TipoOrigen"
            FROM "MovimientosProducto" WHERE "TipoOrigen" = 1 AND "ClaveOrigen" = @N ORDER BY "Id"
            """,
            new { N = numeroRegistro })];
    }

    private async Task<List<decimal>> ImportesValorAsync(string numeroRegistro)
    {
        await using var conexion = _prueba.NuevaConexion();
        return [.. await conexion.QueryAsync<decimal>(
            """SELECT "ImporteCosto" FROM "MovimientosValor" WHERE "TipoOrigen" = 1 AND "ClaveOrigen" = @N""",
            new { N = numeroRegistro })];
    }

    private async Task<List<RegistroDiario>> RegistrosDelLoteAsync(Guid loteId)
    {
        await using var conexion = _prueba.NuevaConexion();
        return [.. await conexion.QueryAsync<RegistroDiario>(
            """
            SELECT "Id", "NumeroRegistro", "LoteDiarioId", "DesdeMovimientoProducto", "HastaMovimientoProducto", "Lineas",
                   "CreadoPor", "UsuarioId"
            FROM "RegistrosDiario" WHERE "LoteDiarioId" = @Id
            """, new { Id = loteId })];
    }

    private Task<int> LineasVivasAsync(Guid loteId) => ConsultarEscalarAsync<int>(
        """SELECT COUNT(*)::int FROM "LineasDiario" WHERE "LoteDiarioId" = @Id AND "IsDeleted" = false""", new { Id = loteId });

    private Task<int> LineasBorradasAsync(Guid loteId) => ConsultarEscalarAsync<int>(
        """
        SELECT COUNT(*)::int FROM "LineasDiario"
        WHERE "LoteDiarioId" = @Id AND "IsDeleted" = true AND "DeletedAtUtc" IS NOT NULL AND "DeletedBy" IS NOT NULL
        """,
        new { Id = loteId });

    private Task<bool> CostoAjustadoAsync(Guid productoId) =>
        ConsultarEscalarAsync<bool>("""SELECT "CostoAjustado" FROM "Productos" WHERE "Id" = @Id""", new { Id = productoId });

    private Task<bool> AlmacenBorradoAsync(Guid almacenId) =>
        ConsultarEscalarAsync<bool>("""SELECT "IsDeleted" FROM "Almacenes" WHERE "Id" = @Id""", new { Id = almacenId });

    private Task<bool> ProductoBorradoAsync(Guid productoId) =>
        ConsultarEscalarAsync<bool>("""SELECT "IsDeleted" FROM "Productos" WHERE "Id" = @Id""", new { Id = productoId });

    private async Task<T> ConsultarEscalarAsync<T>(string sql, object parametros)
    {
        await using var conexion = _prueba.NuevaConexion();
        return await conexion.ExecuteScalarAsync<T>(sql, parametros) ?? throw new InvalidOperationException("Consulta sin fila.");
    }

    private async Task EjecutarSqlAsync(string sql, object parametros)
    {
        await using var conexion = _prueba.NuevaConexion();
        await conexion.ExecuteAsync(sql, parametros);
    }

    private sealed class MovimientoFila
    {
        public long Id { get; init; }
        public Guid ProductoId { get; init; }
        public Guid AlmacenId { get; init; }
        public TipoMovimientoInventario TipoMovimiento { get; init; }
        public TipoDocumentoInventario TipoDocumento { get; init; }
        public string? NumeroDocumento { get; init; }
        public int NumeroLineaDocumento { get; init; }
        public decimal Cantidad { get; init; }
        public TipoOrigenMovimiento TipoOrigen { get; init; }
    }
}
