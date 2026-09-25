using Moq;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Commands;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Handlers;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Inventario;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Features.DiariosInventario.Lotes.Handlers;

public class CreateLoteDiarioCommandHandlerTests
{
    [Fact]
    public async Task Handle_PlantillaInexistente_DevuelveFalloSinGuardar()
    {
        var unitOfWork = ArmarUnitOfWork(out var lotes, out _, out _);

        var handler = new CreateLoteDiarioCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(
            new CreateLoteDiarioCommand(Guid.NewGuid(), "LOTE1", "Lote de prueba", null, false), default);

        Assert.True(result.EsFallo);
        Assert.Equal("diario.plantilla_invalida", result.Errores[0].Codigo);
        lotes.Mock.Verify(r => r.AddAsync(It.IsAny<LoteDiario>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SerieInexistente_DevuelveFalloSinGuardar()
    {
        var unitOfWork = ArmarUnitOfWork(out var lotes, out var plantillas, out _);
        var plantilla = plantillas.Agregar(new PlantillaDiario { Codigo = "ARTICULO", Nombre = "Artículo", Tipo = TipoPlantillaDiario.Articulo });

        var handler = new CreateLoteDiarioCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(
            new CreateLoteDiarioCommand(plantilla.Id, "LOTE1", "Lote de prueba", Guid.NewGuid(), false), default);

        Assert.True(result.EsFallo);
        Assert.Equal("diario.serie_invalida", result.Errores[0].Codigo);
        lotes.Mock.Verify(r => r.AddAsync(It.IsAny<LoteDiario>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CodigoInvalido_DevuelveFalloSinGuardar()
    {
        var unitOfWork = ArmarUnitOfWork(out var lotes, out _, out _);

        var handler = new CreateLoteDiarioCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(
            new CreateLoteDiarioCommand(Guid.NewGuid(), "lote inválido!", "Lote", null, false), default);

        Assert.True(result.EsFallo);
        Assert.Equal("diario.lote_codigo_invalido", result.Errores[0].Codigo);
        lotes.Mock.Verify(r => r.AddAsync(It.IsAny<LoteDiario>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DatosValidos_NormalizaCodigoAMayusculasYGuarda()
    {
        var unitOfWork = ArmarUnitOfWork(out var lotes, out var plantillas, out _);
        var plantilla = plantillas.Agregar(new PlantillaDiario { Codigo = "ARTICULO", Nombre = "Artículo", Tipo = TipoPlantillaDiario.Articulo });

        var handler = new CreateLoteDiarioCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(
            new CreateLoteDiarioCommand(plantilla.Id, " lote1 ", " Lote de prueba ", null, false), default);

        Assert.True(result.EsExito);
        Assert.Equal("LOTE1", result.Valor.Codigo);
        Assert.Equal("Lote de prueba", result.Valor.Nombre);
        Assert.Equal(0, result.Valor.NumeroLineas);
        lotes.Mock.Verify(r => r.AddAsync(It.IsAny<LoteDiario>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IUnitOfWork> ArmarUnitOfWork(
        out RepositorioEnMemoria<LoteDiario> lotes, out RepositorioEnMemoria<PlantillaDiario> plantillas, out RepositorioEnMemoria<Serie> series)
    {
        lotes = new RepositorioEnMemoria<LoteDiario>();
        plantillas = new RepositorioEnMemoria<PlantillaDiario>();
        series = new RepositorioEnMemoria<Serie>();

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<LoteDiario>()).Returns(lotes.Repo);
        unitOfWork.Setup(u => u.Repository<PlantillaDiario>()).Returns(plantillas.Repo);
        unitOfWork.Setup(u => u.Repository<Serie>()).Returns(series.Repo);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return unitOfWork;
    }
}
