using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.SociosNegocio.Commands;
using OpenSource1.Application.Features.SociosNegocio.Handlers;
using OpenSource1.Core.Entities;

namespace OpenSource1.SmokeTests.Features.SociosNegocio.Handlers;

public class DeleteSocioNegocioCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFalse_WhenSocioNegocioDoesNotExist()
    {
        var repo = new Mock<IGenericRepository<SocioDeNegocio>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((SocioDeNegocio?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<SocioDeNegocio>()).Returns(repo.Object);

        var handler = new DeleteSocioNegocioCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteSocioNegocioCommand(Guid.NewGuid()), default);

        Assert.False(result);
    }

    [Fact]
    public async Task Handle_RemovesAndSaves_WhenSocioNegocioExists()
    {
        var entity = new SocioDeNegocio { Nombre = "Test", Apellido = "Client", Email = "test@mail.com" };
        var repo = new Mock<IGenericRepository<SocioDeNegocio>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<SocioDeNegocio>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new DeleteSocioNegocioCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteSocioNegocioCommand(entity.Id), default);

        Assert.True(result);
        repo.Verify(r => r.Remove(entity), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
