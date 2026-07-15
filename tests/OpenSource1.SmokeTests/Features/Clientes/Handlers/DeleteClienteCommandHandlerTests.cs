using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Clientes.Commands;
using OpenSource1.Application.Features.Clientes.Handlers;
using OpenSource1.Core.Entities;

namespace OpenSource1.SmokeTests.Features.Clientes.Handlers;

public class DeleteClienteCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFalse_WhenClienteDoesNotExist()
    {
        var repo = new Mock<IGenericRepository<Cliente>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((Cliente?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Cliente>()).Returns(repo.Object);

        var handler = new DeleteClienteCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteClienteCommand(Guid.NewGuid()), default);

        Assert.False(result);
    }

    [Fact]
    public async Task Handle_RemovesAndSaves_WhenClienteExists()
    {
        var entity = new Cliente { Nombre = "Test", Apellido = "Client", Email = "test@mail.com" };
        var repo = new Mock<IGenericRepository<Cliente>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Cliente>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new DeleteClienteCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteClienteCommand(entity.Id), default);

        Assert.True(result);
        repo.Verify(r => r.Remove(entity), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
