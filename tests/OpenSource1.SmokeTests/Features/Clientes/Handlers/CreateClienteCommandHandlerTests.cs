using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Clientes.Commands;
using OpenSource1.Application.Features.Clientes.Handlers;
using OpenSource1.Core.Entities;

namespace OpenSource1.SmokeTests.Features.Clientes.Handlers;

public class CreateClienteCommandHandlerTests
{
    [Fact]
    public async Task Handle_AddsEntity_SavesAndReturnsMappedResponse()
    {
        var repo = new Mock<IGenericRepository<Cliente>>();
        Cliente? added = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Cliente>(), It.IsAny<CancellationToken>()))
            .Callback<Cliente, CancellationToken>((entity, _) => added = entity)
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Cliente>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateClienteCommandHandler(unitOfWork.Object);
        var response = await handler.Handle(new CreateClienteCommand("  Juan Perez  ", " 001-123 ", "  JP@MAIL.COM ", " 809-111 ", " Calle 1 ", true), default);

        Assert.NotNull(added);
        Assert.Equal("Juan Perez", added!.NombreCompleto);
        Assert.Equal("001-123", added.DocumentoIdentidad);
        Assert.Equal("JP@MAIL.COM", added.Email);
        Assert.Equal("809-111", added.Telefono);
        Assert.Equal("Calle 1", added.Direccion);
        Assert.True(added.Activo);
        Assert.Equal(added.Id, response.Id);
        Assert.Equal("Juan Perez", response.NombreCompleto);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
