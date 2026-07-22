using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Productos.Commands;
using OpenSource1.Application.Features.Productos.Handlers;
using OpenSource1.Core.Entities;
using OpenSource1.Core.ValueObjects;

namespace OpenSource1.SmokeTests.Features.Productos.Handlers;

public class DeleteProductoCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFalse_WhenProductoDoesNotExist()
    {
        var repo = new Mock<IGenericRepository<Producto>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((Producto?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Producto>()).Returns(repo.Object);

        var handler = new DeleteProductoCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteProductoCommand(Guid.NewGuid()), default);

        Assert.False(result);
    }

    [Fact]
    public async Task Handle_RemovesAndSaves_WhenProductoExists()
    {
        var entity = new Producto { Codigo = "COD", Nombre = "Test", Precio = 1, Stock = 1, Categoria = new("BASE", "Base"), UnidadMedida = UnidadMedida.Of("UND") };
        var repo = new Mock<IGenericRepository<Producto>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Producto>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new DeleteProductoCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteProductoCommand(entity.Id), default);

        Assert.True(result);
        repo.Verify(r => r.Remove(entity), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
