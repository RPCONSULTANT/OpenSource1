using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.UnidadesMedida.Commands;
using OpenSource1.Application.Features.UnidadesMedida.Handlers;
using OpenSource1.Core.Entities;

namespace OpenSource1.SmokeTests.Features.UnidadesMedida.Handlers;

public class CreateUnidadMedidaCommandHandlerTests
{
    [Fact]
    public async Task Handle_AddsEntity_SavesAndReturnsMappedResponse()
    {
        var repo = new Mock<IGenericRepository<UnidadMedida>>();
        UnidadMedida? added = null;
        repo.Setup(r => r.AddAsync(It.IsAny<UnidadMedida>(), It.IsAny<CancellationToken>()))
            .Callback<UnidadMedida, CancellationToken>((entity, _) => added = entity)
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<UnidadMedida>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateUnidadMedidaCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new CreateUnidadMedidaCommand("  qq  ", "  Quintal  ", 2), default);

        Assert.True(result.EsExito);
        Assert.NotNull(added);
        Assert.Equal("QQ", added!.Codigo);
        Assert.Equal("Quintal", added.Nombre);
        Assert.Equal(2, added.Decimales);
        Assert.Equal(added.Id, result.Valor.Id);
        Assert.Equal("QQ", result.Valor.Codigo);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("", "Nombre", 0)]
    [InlineData("CODIGOMUYLARGO", "Nombre", 0)]
    [InlineData("COD", "", 0)]
    [InlineData("COD", "Nombre", -1)]
    [InlineData("COD", "Nombre", 7)]
    public async Task Handle_ReturnsFallo_WhenDatosInvalidos(string codigo, string nombre, short decimales)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new CreateUnidadMedidaCommandHandler(unitOfWork.Object);

        var result = await handler.Handle(new CreateUnidadMedidaCommand(codigo, nombre, decimales), default);

        Assert.True(result.EsFallo);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
