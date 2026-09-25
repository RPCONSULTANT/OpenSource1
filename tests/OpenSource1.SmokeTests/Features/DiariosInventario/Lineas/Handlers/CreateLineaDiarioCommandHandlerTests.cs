using Moq;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Commands;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Handlers;
using OpenSource1.Application.Features.DiariosInventario.Lotes;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Inventario;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Features.DiariosInventario.Lineas.Handlers;

public class CreateLineaDiarioCommandHandlerTests
{
    private static readonly DateOnly Hoy = new(2026, 9, 25);

    [Fact]
    public async Task Handle_LoteInexistente_Devuelve404NoEncontrado()
    {
        // Ronda de corrección 1: LoteDiarioId viene de la URL (POST lotes/{loteId}/lineas), así que "no existe" es
        // un 404 -- distinto de "existe pero está bloqueado" (400), que sigue siendo diario.lote_bloqueado.
        var e = ArmarEscenario();

        var result = await Ejecutar(e, e.ComandoBase with { LoteDiarioId = Guid.NewGuid() });

        Assert.True(result.EsFallo);
        Assert.Equal("diario_lote.no_encontrado", result.Errores[0].Codigo);
        Assert.Equal("LoteDiarioId", result.Errores[0].Campo);
    }

    [Fact]
    public async Task Handle_LoteBloqueado_DevuelveLoteBloqueado()
    {
        var e = ArmarEscenario();
        e.Lote.Bloqueado = true;

        var result = await Ejecutar(e, e.ComandoBase);

        Assert.Equal("diario.lote_bloqueado", result.Errores[0].Codigo);
    }

    [Fact]
    public async Task Handle_TipoMovimientoNoPermitidoPorLaPlantilla_DevuelveTipoInvalido()
    {
        var e = ArmarEscenario(); // Articulo: admite 3/4, no 5.

        var result = await Ejecutar(e, e.ComandoBase with { TipoMovimiento = TipoMovimientoInventario.Transferencia, AlmacenDestinoId = e.AlmacenDestino.Id });

        Assert.Equal("diario.tipo_movimiento_invalido", result.Errores[0].Codigo);
        Assert.Equal("TipoMovimiento", result.Errores[0].Campo);
    }

    [Fact]
    public async Task Handle_ReclasificacionConAjustePositivo_DevuelveTipoInvalido()
    {
        var e = ArmarEscenario(TipoPlantillaDiario.Reclasificacion);

        var result = await Ejecutar(e, e.ComandoBase with { TipoMovimiento = TipoMovimientoInventario.AjustePositivo, CostoUnitario = 1m });

        Assert.Equal("diario.tipo_movimiento_invalido", result.Errores[0].Codigo);
    }

    [Fact]
    public async Task Handle_ProductoInexistente_DevuelveProductoInvalido()
    {
        var e = ArmarEscenario();

        var result = await Ejecutar(e, e.ComandoBase with { ProductoId = Guid.NewGuid() });

        Assert.Equal("diario.producto_invalido", result.Errores[0].Codigo);
        Assert.Equal("ProductoId", result.Errores[0].Campo);
    }

    [Fact]
    public async Task Handle_ProductoBloqueadoTodo_DevuelveProductoInvalido()
    {
        var e = ArmarEscenario();
        e.Producto.Bloqueado = BloqueoProducto.Todo;

        var result = await Ejecutar(e, e.ComandoBase);

        Assert.Equal("diario.producto_invalido", result.Errores[0].Codigo);
    }

    [Fact]
    public async Task Handle_ProductoBloqueadoSoloVenta_NoLoRechaza()
    {
        var e = ArmarEscenario();
        e.Producto.Bloqueado = BloqueoProducto.Venta;

        var result = await Ejecutar(e, e.ComandoBase);

        Assert.True(result.EsExito);
    }

    [Fact]
    public async Task Handle_AlmacenInexistente_DevuelveAlmacenInvalido()
    {
        var e = ArmarEscenario();

        var result = await Ejecutar(e, e.ComandoBase with { AlmacenId = Guid.NewGuid() });

        Assert.Equal("diario.almacen_invalido", result.Errores[0].Codigo);
        Assert.Equal("AlmacenId", result.Errores[0].Campo);
    }

    [Fact]
    public async Task Handle_AlmacenBloqueado_DevuelveAlmacenInvalido()
    {
        var e = ArmarEscenario();
        e.Almacen.Bloqueado = true;

        var result = await Ejecutar(e, e.ComandoBase);

        Assert.Equal("diario.almacen_invalido", result.Errores[0].Codigo);
    }

    [Fact]
    public async Task Handle_TransferenciaSinAlmacenDestino_DevuelveDestinoInvalido()
    {
        var e = ArmarEscenario(TipoPlantillaDiario.Reclasificacion);

        var result = await Ejecutar(
            e, e.ComandoBase with { TipoMovimiento = TipoMovimientoInventario.Transferencia, CostoUnitario = null, AlmacenDestinoId = null });

        Assert.Equal("diario.almacen_destino_invalido", result.Errores[0].Codigo);
        Assert.Equal("AlmacenDestinoId", result.Errores[0].Campo);
    }

    [Fact]
    public async Task Handle_TransferenciaConDestinoIgualAlOrigen_DevuelveDestinoInvalido()
    {
        var e = ArmarEscenario(TipoPlantillaDiario.Reclasificacion);

        var result = await Ejecutar(
            e, e.ComandoBase with { TipoMovimiento = TipoMovimientoInventario.Transferencia, CostoUnitario = null, AlmacenDestinoId = e.Almacen.Id });

        Assert.Equal("diario.almacen_destino_invalido", result.Errores[0].Codigo);
    }

    [Fact]
    public async Task Handle_TransferenciaConDestinoInexistente_DevuelveAlmacenInvalidoEnDestino()
    {
        var e = ArmarEscenario(TipoPlantillaDiario.Reclasificacion);

        var result = await Ejecutar(
            e, e.ComandoBase with { TipoMovimiento = TipoMovimientoInventario.Transferencia, CostoUnitario = null, AlmacenDestinoId = Guid.NewGuid() });

        Assert.Equal("diario.almacen_invalido", result.Errores[0].Codigo);
        Assert.Equal("AlmacenDestinoId", result.Errores[0].Campo);
    }

    [Fact]
    public async Task Handle_AjustePositivoConAlmacenDestinoInformado_DevuelveDestinoInvalido()
    {
        var e = ArmarEscenario();

        var result = await Ejecutar(e, e.ComandoBase with { AlmacenDestinoId = e.AlmacenDestino.Id });

        Assert.Equal("diario.almacen_destino_invalido", result.Errores[0].Codigo);
    }

    [Fact]
    public async Task Handle_CantidadCero_DevuelveCantidadInvalida()
    {
        var e = ArmarEscenario();

        var result = await Ejecutar(e, e.ComandoBase with { Cantidad = 0m });

        Assert.Equal("diario.cantidad_invalida", result.Errores[0].Codigo);
        Assert.Equal("Cantidad", result.Errores[0].Campo);
    }

    [Fact]
    public async Task Handle_CantidadConMasDeSeisDecimales_DevuelveCantidadInvalida()
    {
        var e = ArmarEscenario();

        var result = await Ejecutar(e, e.ComandoBase with { Cantidad = 1.1234567m });

        Assert.Equal("diario.cantidad_invalida", result.Errores[0].Codigo);
    }

    [Fact]
    public async Task Handle_AjustePositivoSinCosto_DevuelveCostoRequerido()
    {
        var e = ArmarEscenario();

        var result = await Ejecutar(e, e.ComandoBase with { CostoUnitario = null });

        Assert.Equal("diario.costo_requerido", result.Errores[0].Codigo);
        Assert.Equal("CostoUnitario", result.Errores[0].Campo);
    }

    [Fact]
    public async Task Handle_AjustePositivoConCostoNegativo_DevuelveCostoInvalido()
    {
        var e = ArmarEscenario();

        var result = await Ejecutar(e, e.ComandoBase with { CostoUnitario = -1m });

        Assert.Equal("diario.costo_invalido", result.Errores[0].Codigo);
    }

    [Fact]
    public async Task Handle_AjusteNegativoConCostoInformado_DevuelveCostoInvalido()
    {
        var e = ArmarEscenario();

        var result = await Ejecutar(e, e.ComandoBase with { TipoMovimiento = TipoMovimientoInventario.AjusteNegativo, CostoUnitario = 1m });

        Assert.Equal("diario.costo_invalido", result.Errores[0].Codigo);
    }

    [Fact]
    public async Task Handle_FactorDeConversionFalla_PropagaErrorConCampoUnidadMedidaId()
    {
        var e = ArmarEscenario();
        e.Conversion.Setup(c => c.ObtenerFactorAsync(e.Producto.Id, e.Unidad.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<decimal>.Fallo(new Error("conversion.unidad_no_asociada", "mensaje", "OtroCampo")));

        var result = await Ejecutar(e, e.ComandoBase);

        Assert.Equal("conversion.unidad_no_asociada", result.Errores[0].Codigo);
        Assert.Equal("UnidadMedidaId", result.Errores[0].Campo);
    }

    [Fact]
    public async Task Handle_NumeroDocumentoDemasiadoLargo_DevuelveInvalido()
    {
        var e = ArmarEscenario();

        var result = await Ejecutar(e, e.ComandoBase with { NumeroDocumento = new string('A', 21) });

        Assert.Equal("diario.numero_documento_invalido", result.Errores[0].Codigo);
    }

    [Fact]
    public async Task Handle_DescripcionDemasiadoLarga_DevuelveInvalida()
    {
        var e = ArmarEscenario();

        var result = await Ejecutar(e, e.ComandoBase with { Descripcion = new string('A', 201) });

        Assert.Equal("diario.descripcion_invalida", result.Errores[0].Codigo);
    }

    [Fact]
    public async Task Handle_AjustePositivoValido_CalculaImporteCostoYNumeroLinea10000()
    {
        var e = ArmarEscenario();

        var result = await Ejecutar(e, e.ComandoBase with { Cantidad = 10m, CostoUnitario = 5m });

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Codigo : "");
        Assert.Equal(10000, result.Valor.NumeroLinea);
        Assert.Equal(50m, result.Valor.ImporteCosto);
        Assert.Equal(1m, result.Valor.CantidadPorUnidadMedida);
        Assert.Equal(e.Producto.Codigo, result.Valor.ProductoCodigo);
        Assert.Equal(e.Almacen.Codigo, result.Valor.AlmacenCodigo);
        Assert.Equal(e.Unidad.Codigo, result.Valor.UnidadMedidaCodigo);
    }

    [Fact]
    public async Task Handle_AjusteNegativo_ImporteCostoEsCero()
    {
        var e = ArmarEscenario();

        var result = await Ejecutar(e, e.ComandoBase with { TipoMovimiento = TipoMovimientoInventario.AjusteNegativo, CostoUnitario = null });

        Assert.True(result.EsExito);
        Assert.Equal(0m, result.Valor.ImporteCosto);
    }

    [Fact]
    public async Task Handle_SegundaLinea_NumeroLineaEsMaximoMas10000()
    {
        var e = ArmarEscenario();
        e.Lineas.Agregar(new LineaDiario
        {
            LoteDiarioId = e.Lote.Id, NumeroLinea = 10000, ProductoId = e.Producto.Id, AlmacenId = e.Almacen.Id, UnidadMedidaId = e.Unidad.Id, Cantidad = 1
        });

        var result = await Ejecutar(e, e.ComandoBase);

        Assert.True(result.EsExito);
        Assert.Equal(20000, result.Valor.NumeroLinea);
    }

    [Fact]
    public async Task Handle_LoteConMilLineas_DevuelveLimiteYNoGuarda()
    {
        var e = ArmarEscenario();
        for (var i = 0; i < CreateLineaDiarioCommandHandler.MaximoLineasPorLote; i++)
        {
            e.Lineas.Agregar(new LineaDiario
            {
                LoteDiarioId = e.Lote.Id, NumeroLinea = (i + 1) * 10000, ProductoId = e.Producto.Id, AlmacenId = e.Almacen.Id,
                UnidadMedidaId = e.Unidad.Id, Cantidad = 1
            });
        }

        var result = await Ejecutar(e, e.ComandoBase);

        Assert.True(result.EsFallo);
        Assert.Equal("diario.lote_limite_lineas", result.Errores[0].Codigo);
        e.Lineas.Mock.Verify(r => r.AddAsync(It.IsAny<LineaDiario>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_FactorDistintoDeUno_CongelaCantidadPorUnidadMedidaYCalculaImporte()
    {
        // factor 12 (p. ej. una "caja" de 12 unidades), cantidad 2.5 cajas, costo 1.2345 (unidad base) ->
        // ImporteCosto = 2.5 * 12 * 1.2345 = 37.035.
        var e = ArmarEscenario();
        e.Conversion.Setup(c => c.ObtenerFactorAsync(e.Producto.Id, e.Unidad.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<decimal>.Exito(12m));

        var result = await Ejecutar(e, e.ComandoBase with { Cantidad = 2.5m, CostoUnitario = 1.2345m });

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Codigo : "");
        Assert.Equal(12m, result.Valor.CantidadPorUnidadMedida);
        Assert.Equal(37.035m, result.Valor.ImporteCosto);
    }

    [Fact]
    public async Task Handle_FactorConMasDeSeisDecimales_CongelaElFactorRedondeadoA6Decimales()
    {
        // Mismo criterio que RegistroMovimientosInventario: el factor se redondea a 6 decimales (away from zero)
        // antes de congelarlo, nunca se guarda con más precisión de la que admite la columna.
        var e = ArmarEscenario();
        e.Conversion.Setup(c => c.ObtenerFactorAsync(e.Producto.Id, e.Unidad.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<decimal>.Exito(3.12345675m));

        var result = await Ejecutar(e, e.ComandoBase);

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Codigo : "");
        Assert.Equal(3.123457m, result.Valor.CantidadPorUnidadMedida);
    }

    [Fact]
    public async Task Handle_CantidadMayorQueElMaximoDeNumeric18_6_DevuelveCantidadInvalida()
    {
        var e = ArmarEscenario();

        var result = await Ejecutar(e, e.ComandoBase with { Cantidad = 1_000_000_000_000m, CostoUnitario = 1m });

        Assert.True(result.EsFallo);
        Assert.Equal("diario.cantidad_invalida", result.Errores[0].Codigo);
        Assert.Equal("Cantidad", result.Errores[0].Campo);
    }

    [Fact]
    public async Task Handle_CantidadBaseDesbordaNumeric18_6_PorElFactor_DevuelveCantidadInvalida()
    {
        // Cantidad por sí sola es válida (900 mil millones < máximo), pero factor 2 la lleva a 1.8 billones, que ya
        // no cabe en numeric(18,6): sin esta comprobación, el registro (Task 4.3) reventaría con 22003 al postear.
        var e = ArmarEscenario();
        e.Conversion.Setup(c => c.ObtenerFactorAsync(e.Producto.Id, e.Unidad.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<decimal>.Exito(2m));

        var result = await Ejecutar(e, e.ComandoBase with { Cantidad = 900_000_000_000m, CostoUnitario = 1m });

        Assert.True(result.EsFallo);
        Assert.Equal("diario.cantidad_invalida", result.Errores[0].Codigo);
        Assert.Equal("Cantidad", result.Errores[0].Campo);
    }

    [Fact]
    public async Task Handle_ImporteCostoDesbordaNumeric18_4_DevuelveCostoInvalidoConCampoCostoUnitario()
    {
        // Cantidad 1e6 y costo 1e10 pasan sus propios límites individuales, pero el PRODUCTO (1e16) desborda
        // numeric(18,4) igual.
        var e = ArmarEscenario();

        var result = await Ejecutar(e, e.ComandoBase with { Cantidad = 1_000_000m, CostoUnitario = 10_000_000_000m });

        Assert.True(result.EsFallo);
        Assert.Equal("diario.costo_invalido", result.Errores[0].Codigo);
        Assert.Equal("CostoUnitario", result.Errores[0].Campo);
    }

    [Fact]
    public async Task Handle_FechaRegistroPorDefecto_DevuelveFechaInvalida()
    {
        var e = ArmarEscenario();

        var result = await Ejecutar(e, e.ComandoBase with { FechaRegistro = default });

        Assert.True(result.EsFallo);
        Assert.Equal("diario.fecha_invalida", result.Errores[0].Codigo);
        Assert.Equal("FechaRegistro", result.Errores[0].Campo);
    }

    [Fact]
    public async Task Handle_FechaDocumentoPorDefecto_DevuelveFechaInvalida()
    {
        var e = ArmarEscenario();

        var result = await Ejecutar(e, e.ComandoBase with { FechaDocumento = default });

        Assert.True(result.EsFallo);
        Assert.Equal("diario.fecha_invalida", result.Errores[0].Codigo);
        Assert.Equal("FechaDocumento", result.Errores[0].Campo);
    }

    private static async Task<Result<LineaDiarioResponse>> Ejecutar(
        Escenario e, CreateLineaDiarioCommand comando)
    {
        var handler = new CreateLineaDiarioCommandHandler(e.UnitOfWork.Object, e.LoteBloqueo.Object, e.Conversion.Object);
        return await handler.Handle(comando, default);
    }

    private static Escenario ArmarEscenario(TipoPlantillaDiario tipoPlantilla = TipoPlantillaDiario.Articulo)
    {
        var plantillas = new RepositorioEnMemoria<PlantillaDiario>();
        var plantilla = plantillas.Agregar(new PlantillaDiario { Codigo = "P", Nombre = "P", Tipo = tipoPlantilla });

        var lotes = new RepositorioEnMemoria<LoteDiario>();
        var lote = lotes.Agregar(new LoteDiario { PlantillaDiarioId = plantilla.Id, Codigo = "L1", Nombre = "Lote 1" });

        var productos = new RepositorioEnMemoria<Producto>();
        var producto = productos.Agregar(new Producto
        {
            Codigo = "PROD1", Nombre = "Producto 1", UnidadMedidaBaseId = Guid.NewGuid(), CategoriaId = Guid.NewGuid()
        });

        var almacenes = new RepositorioEnMemoria<Almacen>();
        var almacen = almacenes.Agregar(new Almacen { Codigo = "A1", Nombre = "Almacén 1" });
        var almacenDestino = almacenes.Agregar(new Almacen { Codigo = "A2", Nombre = "Almacén 2" });

        var unidades = new RepositorioEnMemoria<UnidadMedida>();
        var unidad = unidades.Agregar(new UnidadMedida { Codigo = "UND", Nombre = "Unidad" });

        var lineas = new RepositorioEnMemoria<LineaDiario>();

        var conversion = new Mock<IConversionUnidadMedidaService>();
        conversion.Setup(c => c.ObtenerFactorAsync(producto.Id, unidad.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<decimal>.Exito(1m));

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<LoteDiario>()).Returns(lotes.Repo);
        unitOfWork.Setup(u => u.Repository<PlantillaDiario>()).Returns(plantillas.Repo);
        unitOfWork.Setup(u => u.Repository<Producto>()).Returns(productos.Repo);
        unitOfWork.Setup(u => u.Repository<Almacen>()).Returns(almacenes.Repo);
        unitOfWork.Setup(u => u.Repository<UnidadMedida>()).Returns(unidades.Repo);
        unitOfWork.Setup(u => u.Repository<LineaDiario>()).Returns(lineas.Repo);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<IAsyncDisposable>());
        unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Evalúa dinámicamente el estado vigente de "lotes.Datos": null si no hay un lote vivo con ese Id, si no su
        // Bloqueado -- mismo contrato que LoteDiarioBloqueoService.BloquearYObtenerEstadoAsync contra Postgres real.
        var loteBloqueo = new Mock<ILoteDiarioBloqueoService>();
        loteBloqueo.Setup(s => s.BloquearYObtenerEstadoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                lotes.Datos.Where(l => !l.IsDeleted && l.Id == id).Select(l => (bool?)l.Bloqueado).FirstOrDefault());

        var comandoBase = new CreateLineaDiarioCommand(
            lote.Id, Hoy, Hoy, "DOC-1", TipoMovimientoInventario.AjustePositivo, producto.Id, almacen.Id, null, unidad.Id,
            1m, 1m, "Descripción de prueba");

        return new Escenario(unitOfWork, loteBloqueo, lineas, lote, producto, almacen, almacenDestino, unidad, conversion, comandoBase);
    }

    private sealed record Escenario(
        Mock<IUnitOfWork> UnitOfWork,
        Mock<ILoteDiarioBloqueoService> LoteBloqueo,
        RepositorioEnMemoria<LineaDiario> Lineas,
        LoteDiario Lote,
        Producto Producto,
        Almacen Almacen,
        Almacen AlmacenDestino,
        UnidadMedida Unidad,
        Mock<IConversionUnidadMedidaService> Conversion,
        CreateLineaDiarioCommand ComandoBase);
}
