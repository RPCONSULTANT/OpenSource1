using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Productos.Commands;
using OpenSource1.Application.Features.Productos.Handlers;
using OpenSource1.Core.Entities;

namespace OpenSource1.SmokeTests.Features.Productos.Handlers;

public class UpdateProductoCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsNull_WhenProductoDoesNotExist()
    {
        var repo = new Mock<IGenericRepository<Producto>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((Producto?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Producto>()).Returns(repo.Object);

        var handler = new UpdateProductoCommandHandler(unitOfWork.Object);
        var response = await handler.Handle(new UpdateProductoCommand(Guid.NewGuid(), "C", "N", 1, 1, "Cat"), default);

        Assert.Null(response);
    }

    [Fact]
    public async Task Handle_UpdatesAndSaves_WhenProductoExists()
    {
        var entity = new Producto { Codigo = "OLD", Nombre = "Old", Precio = 1, Stock = 1, Categoria = "Base" };
        var repo = new Mock<IGenericRepository<Producto>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Producto>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateProductoCommandHandler(unitOfWork.Object);
        var response = await handler.Handle(new UpdateProductoCommand(entity.Id, " NEW ", " Nuevo ", 3.25m, 9, " General "), default);

        Assert.NotNull(response);
        Assert.Equal("NEW", entity.Codigo);
        Assert.Equal("Nuevo", entity.Nombre);
        Assert.Equal(3.25m, entity.Precio);
        Assert.Equal(9, entity.Stock);
        Assert.Equal("General", entity.Categoria);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
