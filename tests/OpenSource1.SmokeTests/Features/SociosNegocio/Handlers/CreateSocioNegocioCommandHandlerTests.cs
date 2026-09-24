using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.SociosNegocio.Commands;
using OpenSource1.Application.Features.SociosNegocio.Handlers;
using OpenSource1.Core.Entities;

namespace OpenSource1.SmokeTests.Features.SociosNegocio.Handlers;

public class CreateSocioNegocioCommandHandlerTests
{
    [Fact]
    public async Task Handle_AddsEntity_SavesAndReturnsMappedResponse()
    {
        var repo = new Mock<IGenericRepository<SocioDeNegocio>>();
        SocioDeNegocio? added = null;
        repo.Setup(r => r.AddAsync(It.IsAny<SocioDeNegocio>(), It.IsAny<CancellationToken>()))
            .Callback<SocioDeNegocio, CancellationToken>((entity, _) => added = entity)
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<SocioDeNegocio>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateSocioNegocioCommandHandler(unitOfWork.Object);
        var response = await handler.Handle(new CreateSocioNegocioCommand("  Juan  ", "  Perez  ", "  JP@MAIL.COM ", " 809-111 ", " Calle 1 ", null, null, null), default);

        Assert.NotNull(added);
        Assert.Equal("Juan", added!.Nombre);
        Assert.Equal("Perez", added.Apellido);
        Assert.Equal("JP@MAIL.COM", added.Email);
        Assert.Equal("809-111", added.Telefono);
        Assert.Equal("Calle 1", added.Direccion?.Linea1);
        Assert.Equal(added.Id, response.Id);
        Assert.Equal("Juan", response.Nombre);
        Assert.Equal("Perez", response.Apellido);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
