using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.TerminosPago.Commands;
using OpenSource1.Application.Features.TerminosPago.Handlers;
using OpenSource1.Core.Entities;

namespace OpenSource1.SmokeTests.Features.TerminosPago.Handlers;

public class CreateTerminoPagoCommandHandlerTests
{
    [Fact]
    public async Task Handle_AddsEntity_SavesAndReturnsMappedResponse()
    {
        var repo = new Mock<IGenericRepository<TerminoPago>>();
        TerminoPago? added = null;
        repo.Setup(r => r.AddAsync(It.IsAny<TerminoPago>(), It.IsAny<CancellationToken>()))
            .Callback<TerminoPago, CancellationToken>((entity, _) => added = entity)
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<TerminoPago>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateTerminoPagoCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(
            new CreateTerminoPagoCommand("  30D  ", "  30 días  ", 30, 10, 2.5m), default);

        Assert.True(result.EsExito);
        Assert.NotNull(added);
        Assert.Equal("30D", added!.Codigo);
        Assert.Equal("30 días", added.Descripcion);
        Assert.Equal(30, added.DiasVencimiento);
        Assert.Equal(added.Id, result.Valor.Id);
        Assert.Equal("30D", result.Valor.Codigo);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("", "Descripción", 0, 0, 0)]
    [InlineData("COD", "", 0, 0, 0)]
    [InlineData("COD", "Descripción", -1, 0, 0)]
    [InlineData("COD", "Descripción", 0, -1, 0)]
    [InlineData("COD", "Descripción", 0, 0, 150)]
    public async Task Handle_ReturnsFallo_WhenDatosInvalidos(
        string codigo, string descripcion, int diasVencimiento, int diasDescuento, decimal porcentajeDescuento)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new CreateTerminoPagoCommandHandler(unitOfWork.Object);

        var result = await handler.Handle(
            new CreateTerminoPagoCommand(codigo, descripcion, diasVencimiento, diasDescuento, porcentajeDescuento), default);

        Assert.True(result.EsFallo);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
