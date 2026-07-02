using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Productos.Commands;
using OpenSource1.Application.Features.Productos.Handlers;
using OpenSource1.Core.Entities;

namespace OpenSource1.SmokeTests.Features.Productos.Handlers;

public class CreateProductoCommandHandlerTests
{
    [Fact]
    public async Task Handle_AddsEntity_SavesAndReturnsMappedResponse()
    {
        var repo = new Mock<IGenericRepository<Producto>>();
        Producto? added = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Producto>(), It.IsAny<CancellationToken>()))
            .Callback<Producto, CancellationToken>((entity, _) => added = entity)
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Producto>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateProductoCommandHandler(unitOfWork.Object);
        var response = await handler.Handle(new CreateProductoCommand("  COD-1 ", "  Producto ", " Desc ", 10.50m, 5, true), default);

        Assert.NotNull(added);
        Assert.Equal("COD-1", added!.Codigo);
        Assert.Equal("Producto", added.Nombre);
        Assert.Equal(10.50m, added.Precio);
        Assert.Equal(5, added.Stock);
        Assert.Equal(response.Id, added.Id);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
