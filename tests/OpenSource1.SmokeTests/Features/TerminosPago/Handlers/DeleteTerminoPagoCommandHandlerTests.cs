using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.TerminosPago.Commands;
using OpenSource1.Application.Features.TerminosPago.Handlers;
using OpenSource1.Core.Entities;

namespace OpenSource1.SmokeTests.Features.TerminosPago.Handlers;

public class DeleteTerminoPagoCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFallo_WhenTerminoPagoDoesNotExist()
    {
        var repo = new Mock<IGenericRepository<TerminoPago>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((TerminoPago?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<TerminoPago>()).Returns(repo.Object);

        var handler = new DeleteTerminoPagoCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteTerminoPagoCommand(Guid.NewGuid()), default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Codigo == "termino_pago.no_encontrado");
    }

    [Fact]
    public async Task Handle_RemovesAndSaves_WhenTerminoPagoExists()
    {
        var entity = new TerminoPago { Codigo = "COD", Descripcion = "Test", DiasVencimiento = 0, DiasDescuento = 0, PorcentajeDescuento = 0m };
        var repo = new Mock<IGenericRepository<TerminoPago>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<TerminoPago>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new DeleteTerminoPagoCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteTerminoPagoCommand(entity.Id), default);

        Assert.True(result.EsExito);
        repo.Verify(r => r.Remove(entity), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
