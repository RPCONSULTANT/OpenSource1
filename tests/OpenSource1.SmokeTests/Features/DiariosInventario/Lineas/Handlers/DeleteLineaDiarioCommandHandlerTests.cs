using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Commands;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Handlers;
using OpenSource1.Application.Features.DiariosInventario.Lotes;
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

        var handler = new DeleteLineaDiarioCommandHandler(unitOfWork.Object, Mock.Of<ILoteDiarioBloqueoService>());
        var result = await handler.Handle(new DeleteLineaDiarioCommand(Guid.NewGuid()), default);

        Assert.True(result.EsFallo);
        Assert.Equal("diario_linea.no_encontrado", result.Errores[0].Codigo);
    }

    [Fact]
    public async Task Handle_LineaExistente_BorraYConfirma()
    {
        var loteId = Guid.NewGuid();
        var entity = new LineaDiario
        {
            LoteDiarioId = loteId, ProductoId = Guid.NewGuid(), AlmacenId = Guid.NewGuid(), UnidadMedidaId = Guid.NewGuid(), Cantidad = 1
        };
        var repo = new Mock<IGenericRepository<LineaDiario>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var unitOfWork = ArmarUnitOfWork(repo);

        var loteBloqueo = new Mock<ILoteDiarioBloqueoService>();
        loteBloqueo.Setup(s => s.BloquearYObtenerEstadoAsync(loteId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var handler = new DeleteLineaDiarioCommandHandler(unitOfWork.Object, loteBloqueo.Object);
        var result = await handler.Handle(new DeleteLineaDiarioCommand(entity.Id), default);

        Assert.True(result.EsExito);
        repo.Verify(r => r.Remove(entity), Times.Once);
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_LoteBloqueado_DevuelveLoteBloqueadoSinBorrar()
    {
        var loteId = Guid.NewGuid();
        var entity = new LineaDiario
        {
            LoteDiarioId = loteId, ProductoId = Guid.NewGuid(), AlmacenId = Guid.NewGuid(), UnidadMedidaId = Guid.NewGuid(), Cantidad = 1
        };
        var repo = new Mock<IGenericRepository<LineaDiario>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var unitOfWork = ArmarUnitOfWork(repo);

        var loteBloqueo = new Mock<ILoteDiarioBloqueoService>();
        loteBloqueo.Setup(s => s.BloquearYObtenerEstadoAsync(loteId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new DeleteLineaDiarioCommandHandler(unitOfWork.Object, loteBloqueo.Object);
        var result = await handler.Handle(new DeleteLineaDiarioCommand(entity.Id), default);

        Assert.True(result.EsFallo);
        Assert.Equal("diario.lote_bloqueado", result.Errores[0].Codigo);
        repo.Verify(r => r.Remove(It.IsAny<LineaDiario>()), Times.Never);
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IUnitOfWork> ArmarUnitOfWork(Mock<IGenericRepository<LineaDiario>> repo)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<LineaDiario>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<IAsyncDisposable>());
        unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return unitOfWork;
    }
}
