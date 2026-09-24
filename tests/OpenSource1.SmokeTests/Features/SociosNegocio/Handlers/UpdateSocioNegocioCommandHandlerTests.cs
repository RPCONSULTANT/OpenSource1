using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.SociosNegocio.Commands;
using OpenSource1.Application.Features.SociosNegocio.Handlers;
using OpenSource1.Core.Entities;

namespace OpenSource1.SmokeTests.Features.SociosNegocio.Handlers;

public class UpdateSocioNegocioCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsNull_WhenSocioNegocioDoesNotExist()
    {
        var repo = new Mock<IGenericRepository<SocioDeNegocio>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((SocioDeNegocio?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<SocioDeNegocio>()).Returns(repo.Object);

        var handler = new UpdateSocioNegocioCommandHandler(unitOfWork.Object);
        var response = await handler.Handle(new UpdateSocioNegocioCommand(Guid.NewGuid(), "A", "B", "C", null, null, null, null, null), default);

        Assert.Null(response);
    }

    [Fact]
    public async Task Handle_UpdatesAndSaves_WhenSocioNegocioExists()
    {
        var entity = new SocioDeNegocio { Nombre = "Old", Apellido = "Client", Email = "old@mail.com" };
        var repo = new Mock<IGenericRepository<SocioDeNegocio>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<SocioDeNegocio>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateSocioNegocioCommandHandler(unitOfWork.Object);
        var response = await handler.Handle(new UpdateSocioNegocioCommand(entity.Id, " Nuevo ", " Apellido ", " nuevo@mail.com ", " 809 ", " Dir ", null, null, null), default);

        Assert.NotNull(response);
        Assert.Equal("Nuevo", entity.Nombre);
        Assert.Equal("Apellido", entity.Apellido);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
