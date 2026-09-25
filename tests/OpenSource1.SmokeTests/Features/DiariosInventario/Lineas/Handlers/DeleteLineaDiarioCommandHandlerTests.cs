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
        var entity = new LineaDiario
        {
            LoteDiarioId = Guid.NewGuid(), ProductoId = Guid.NewGuid(), AlmacenId = Guid.NewGuid(), UnidadMedidaId = Guid.NewGuid(), Cantidad = 1
        };
        var repo = new Mock<IGenericRepository<LineaDiario>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<LineaDiario>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new DeleteLineaDiarioCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteLineaDiarioCommand(entity.Id), default);

        Assert.True(result.EsExito);
        repo.Verify(r => r.Remove(entity), Times.Once);
    }
}
