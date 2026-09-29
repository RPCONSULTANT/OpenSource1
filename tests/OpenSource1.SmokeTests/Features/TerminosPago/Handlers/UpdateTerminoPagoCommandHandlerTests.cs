using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.TerminosPago.Commands;
using OpenSource1.Application.Features.TerminosPago.Handlers;
using OpenSource1.Core.Entities;

namespace OpenSource1.SmokeTests.Features.TerminosPago.Handlers;

public class UpdateTerminoPagoCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFallo_WhenTerminoPagoDoesNotExist()
    {
        var repo = new Mock<IGenericRepository<TerminoPago>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((TerminoPago?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<TerminoPago>()).Returns(repo.Object);

        var handler = new UpdateTerminoPagoCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(
            new UpdateTerminoPagoCommand(Guid.NewGuid(), "COD", "Descripción", 30, 10, 2m), default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Codigo == "termino_pago.no_encontrado");
    }

    [Fact]
    public async Task Handle_UpdatesAndSaves_WhenTerminoPagoExists()
    {
        var entity = new TerminoPago { Codigo = "OLD", Descripcion = "Vieja", DiasVencimiento = 10, DiasDescuento = 5, PorcentajeDescuento = 1m };
        var repo = new Mock<IGenericRepository<TerminoPago>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<TerminoPago>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateTerminoPagoCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(
            new UpdateTerminoPagoCommand(entity.Id, "NEW", "Nueva", 30, 10, 2.5m), default);

        Assert.True(result.EsExito);
        Assert.Equal("NEW", entity.Codigo);
        Assert.Equal("Nueva", entity.Descripcion);
        Assert.Equal(30, entity.DiasVencimiento);
        Assert.Equal("NEW", result.Valor.Codigo);
        repo.Verify(r => r.Update(entity), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsFallo_WhenDatosInvalidos()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new UpdateTerminoPagoCommandHandler(unitOfWork.Object);

        var result = await handler.Handle(
            new UpdateTerminoPagoCommand(Guid.NewGuid(), "", "", -1, -1, 200m), default);

        Assert.True(result.EsFallo);
        unitOfWork.Verify(u => u.Repository<TerminoPago>(), Times.Never);
    }
}
