using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Commands;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Handlers;
using OpenSource1.Core.Entities.Inventario;

namespace OpenSource1.SmokeTests.Features.DiariosInventario.Lotes.Handlers;

public class DeleteLoteDiarioCommandHandlerTests
{
    [Fact]
    public async Task Handle_LoteInexistente_DevuelveFallo()
    {
        var repo = new Mock<IGenericRepository<LoteDiario>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((LoteDiario?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<LoteDiario>()).Returns(repo.Object);

        var handler = new DeleteLoteDiarioCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteLoteDiarioCommand(Guid.NewGuid()), default);

        Assert.True(result.EsFallo);
        Assert.Equal("diario_lote.no_encontrado", result.Errores[0].Codigo);
    }

    [Fact]
    public async Task Handle_ConLineas_DevuelveConflictoSinBorrar()
    {
        var entity = new LoteDiario { PlantillaDiarioId = Guid.NewGuid(), Codigo = "COD", Nombre = "Nombre" };
        var repo = new Mock<IGenericRepository<LoteDiario>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var linea = new LineaDiario
        {
            LoteDiarioId = entity.Id, ProductoId = Guid.NewGuid(), AlmacenId = Guid.NewGuid(), UnidadMedidaId = Guid.NewGuid(), Cantidad = 1
        };
        var lineasRepo = new Mock<IGenericRepository<LineaDiario>>();
        lineasRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LineaDiario, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(linea);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<LoteDiario>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.Repository<LineaDiario>()).Returns(lineasRepo.Object);

        var handler = new DeleteLoteDiarioCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteLoteDiarioCommand(entity.Id), default);

        Assert.True(result.EsFallo);
        Assert.Equal("diario.conflicto", result.Errores[0].Codigo);
        repo.Verify(r => r.Remove(It.IsAny<LoteDiario>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SinLineas_BorraYGuarda()
    {
        var entity = new LoteDiario { PlantillaDiarioId = Guid.NewGuid(), Codigo = "COD", Nombre = "Nombre" };
        var repo = new Mock<IGenericRepository<LoteDiario>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var lineasRepo = new Mock<IGenericRepository<LineaDiario>>();
        lineasRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LineaDiario, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LineaDiario?)null);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<LoteDiario>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.Repository<LineaDiario>()).Returns(lineasRepo.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new DeleteLoteDiarioCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteLoteDiarioCommand(entity.Id), default);

        Assert.True(result.EsExito);
        repo.Verify(r => r.Remove(entity), Times.Once);
    }
}
