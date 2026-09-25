using Moq;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Commands;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Handlers;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Inventario;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Features.DiariosInventario.Lineas.Handlers;

public class UpdateLineaDiarioCommandHandlerTests
{
    private static readonly DateOnly Hoy = new(2026, 9, 25);

    [Fact]
    public async Task Handle_LineaInexistente_DevuelveFallo()
    {
        var plantillas = new RepositorioEnMemoria<PlantillaDiario>();
        var lotes = new RepositorioEnMemoria<LoteDiario>();
        var lineas = new RepositorioEnMemoria<LineaDiario>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<LineaDiario>()).Returns(lineas.Repo);

        var handler = new UpdateLineaDiarioCommandHandler(unitOfWork.Object, Mock.Of<IConversionUnidadMedidaService>());
        var result = await handler.Handle(
            new UpdateLineaDiarioCommand(
                Guid.NewGuid(), Hoy, Hoy, null, TipoMovimientoInventario.AjustePositivo, Guid.NewGuid(), Guid.NewGuid(), null,
                Guid.NewGuid(), 1m, 1m, null, 1),
            default);

        Assert.True(result.EsFallo);
        Assert.Equal("diario_linea.no_encontrado", result.Errores[0].Codigo);
    }

    [Fact]
    public async Task Handle_DatosValidos_EstableceLaVersionOriginalDeXminYRecalculaImporte()
    {
        var plantillas = new RepositorioEnMemoria<PlantillaDiario>();
        var plantilla = plantillas.Agregar(new PlantillaDiario { Codigo = "P", Nombre = "P", Tipo = TipoPlantillaDiario.Articulo });

        var lotes = new RepositorioEnMemoria<LoteDiario>();
        var lote = lotes.Agregar(new LoteDiario { PlantillaDiarioId = plantilla.Id, Codigo = "L1", Nombre = "Lote 1" });

        var productos = new RepositorioEnMemoria<Producto>();
        var producto = productos.Agregar(new Producto { Codigo = "PROD1", Nombre = "P1", UnidadMedidaBaseId = Guid.NewGuid(), CategoriaId = Guid.NewGuid() });

        var almacenes = new RepositorioEnMemoria<Almacen>();
        var almacen = almacenes.Agregar(new Almacen { Codigo = "A1", Nombre = "A1" });

        var unidades = new RepositorioEnMemoria<UnidadMedida>();
        var unidad = unidades.Agregar(new UnidadMedida { Codigo = "UND", Nombre = "Unidad" });

        var lineas = new RepositorioEnMemoria<LineaDiario>();
        var linea = lineas.Agregar(new LineaDiario
        {
            LoteDiarioId = lote.Id, NumeroLinea = 10000, ProductoId = producto.Id, AlmacenId = almacen.Id, UnidadMedidaId = unidad.Id,
            Cantidad = 1, TipoMovimiento = TipoMovimientoInventario.AjustePositivo, CostoUnitario = 1m, FechaRegistro = Hoy, FechaDocumento = Hoy
        });

        var conversion = new Mock<IConversionUnidadMedidaService>();
        conversion.Setup(c => c.ObtenerFactorAsync(producto.Id, unidad.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Result<decimal>.Exito(1m));

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<LineaDiario>()).Returns(lineas.Repo);
        unitOfWork.Setup(u => u.Repository<LoteDiario>()).Returns(lotes.Repo);
        unitOfWork.Setup(u => u.Repository<PlantillaDiario>()).Returns(plantillas.Repo);
        unitOfWork.Setup(u => u.Repository<Producto>()).Returns(productos.Repo);
        unitOfWork.Setup(u => u.Repository<Almacen>()).Returns(almacenes.Repo);
        unitOfWork.Setup(u => u.Repository<UnidadMedida>()).Returns(unidades.Repo);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateLineaDiarioCommandHandler(unitOfWork.Object, conversion.Object);
        var result = await handler.Handle(
            new UpdateLineaDiarioCommand(
                linea.Id, Hoy, Hoy, "DOC-2", TipoMovimientoInventario.AjustePositivo, producto.Id, almacen.Id, null, unidad.Id,
                10m, 5m, "Nueva descripción", 99),
            default);

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Codigo : "");
        Assert.Equal(50m, result.Valor.ImporteCosto);
        Assert.Equal(10000, result.Valor.NumeroLinea); // inmutable
        Assert.Equal(lote.Id, result.Valor.LoteDiarioId); // inmutable
        lineas.Mock.Verify(r => r.EstablecerVersionOriginal(linea, 99), Times.Once);
    }
}
