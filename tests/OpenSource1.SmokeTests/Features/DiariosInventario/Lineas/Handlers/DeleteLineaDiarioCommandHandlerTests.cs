using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Commands;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Handlers;
using OpenSource1.Core.Entities.Inventario;

namespace OpenSource1.SmokeTests.Features.DiariosInventario.Lineas.Handlers;

public class DeleteLineaDiarioCommandHandlerTests
{
    [Fact]
    public async Task Handle_LineaInexistente_DevuelveFallo()
    {
        var repo = new Mock<IGenericRepository<LineaDiario>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((LineaDiario?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<LineaDiario>()).Returns(repo.Object);

        var handler = new DeleteLineaDiarioCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteLineaDiarioCommand(Guid.NewGuid()), default);

        Assert.True(result.EsFallo);
        Assert.Equal("diario_linea.no_encontrado", result.Errores[0].Codigo);
    }

    [Fact]
    public async Task Handle_LineaExistente_BorraYGuarda()
    {
        var lote = new LoteDiario { PlantillaDiarioId = Guid.NewGuid(), Codigo = "L1", Nombre = "Lote 1", Bloqueado = false };
        var entity = new LineaDiario
        {
            LoteDiarioId = lote.Id, ProductoId = Guid.NewGuid(), AlmacenId = Guid.NewGuid(), UnidadMedidaId = Guid.NewGuid(), Cantidad = 1
        };
        var repo = new Mock<IGenericRepository<LineaDiario>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var loteRepo = new Mock<IGenericRepository<LoteDiario>>();
        loteRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<LoteDiario, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lote);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<LineaDiario>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.Repository<LoteDiario>()).Returns(loteRepo.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new DeleteLineaDiarioCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteLineaDiarioCommand(entity.Id), default);

        Assert.True(result.EsExito);
        repo.Verify(r => r.Remove(entity), Times.Once);
    }

    [Fact]
    public async Task Handle_LoteBloqueado_DevuelveLoteBloqueadoSinBorrar()
    {
        var lote = new LoteDiario { PlantillaDiarioId = Guid.NewGuid(), Codigo = "L1", Nombre = "Lote 1", Bloqueado = true };
        var entity = new LineaDiario
        {
            LoteDiarioId = lote.Id, ProductoId = Guid.NewGuid(), AlmacenId = Guid.NewGuid(), UnidadMedidaId = Guid.NewGuid(), Cantidad = 1
        };
        var repo = new Mock<IGenericRepository<LineaDiario>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var loteRepo = new Mock<IGenericRepository<LoteDiario>>();
        loteRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<LoteDiario, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lote);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<LineaDiario>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.Repository<LoteDiario>()).Returns(loteRepo.Object);

        var handler = new DeleteLineaDiarioCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteLineaDiarioCommand(entity.Id), default);

        Assert.True(result.EsFallo);
        Assert.Equal("diario.lote_bloqueado", result.Errores[0].Codigo);
        repo.Verify(r => r.Remove(It.IsAny<LineaDiario>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
