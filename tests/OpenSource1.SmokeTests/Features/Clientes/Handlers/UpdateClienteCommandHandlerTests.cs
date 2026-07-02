using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Clientes.Commands;
using OpenSource1.Application.Features.Clientes.Handlers;
using OpenSource1.Core.Entities;

namespace OpenSource1.SmokeTests.Features.Clientes.Handlers;

public class UpdateClienteCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsNull_WhenClienteDoesNotExist()
    {
        var repo = new Mock<IGenericRepository<Cliente>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((Cliente?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Cliente>()).Returns(repo.Object);

        var handler = new UpdateClienteCommandHandler(unitOfWork.Object);
        var response = await handler.Handle(new UpdateClienteCommand(Guid.NewGuid(), "A", "B", "C", null, null, false), default);

        Assert.Null(response);
    }

    [Fact]
    public async Task Handle_UpdatesAndSaves_WhenClienteExists()
    {
        var entity = new Cliente { NombreCompleto = "Old", DocumentoIdentidad = "1", Email = "old@mail.com", Activo = false };
        var repo = new Mock<IGenericRepository<Cliente>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Cliente>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateClienteCommandHandler(unitOfWork.Object);
        var response = await handler.Handle(new UpdateClienteCommand(entity.Id, " Nuevo ", " 002 ", " nuevo@mail.com ", " 809 ", " Dir ", true), default);

        Assert.NotNull(response);
        Assert.Equal("Nuevo", entity.NombreCompleto);
        Assert.Equal("002", entity.DocumentoIdentidad);
        Assert.True(entity.Activo);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
