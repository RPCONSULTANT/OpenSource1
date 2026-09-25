using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Productos.Commands;
using OpenSource1.Application.Features.Productos.Handlers;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Inventario;

namespace OpenSource1.SmokeTests.Features.Productos.Handlers;

public class DeleteProductoCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFallo_WhenProductoDoesNotExist()
    {
        var repo = new Mock<IGenericRepository<Producto>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((Producto?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Producto>()).Returns(repo.Object);

        var handler = new DeleteProductoCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteProductoCommand(Guid.NewGuid()), default);

        Assert.True(result.EsFallo);
        Assert.Equal("producto.no_encontrado", result.Errores[0].Codigo);
    }

    [Fact]
    public async Task Handle_RemovesAndSaves_WhenProductoExistsYSinMovimientos()
    {
        var entity = new Producto { Codigo = "COD", Nombre = "Test", PrecioVenta = 1, CategoriaId = Guid.NewGuid(), UnidadMedidaBaseId = Guid.NewGuid() };
        var repo = new Mock<IGenericRepository<Producto>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Producto>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.Repository<MovimientoProducto>()).Returns(SinMovimientos());
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new DeleteProductoCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteProductoCommand(entity.Id), default);

        Assert.True(result.EsExito);
        repo.Verify(r => r.Remove(entity), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsConflicto_WhenProductoTieneMovimientosDeInventario()
    {
        // Guarda añadida en la revisión final de la Fase 3, mismo patrón que DeleteAlmacenCommandHandler: el libro
        // de inventario (append-only, sin FK-cascade de borrado) impide que un producto con movimientos desaparezca.
        var entity = new Producto { Codigo = "COD", Nombre = "Test", PrecioVenta = 1, CategoriaId = Guid.NewGuid(), UnidadMedidaBaseId = Guid.NewGuid() };
        var repo = new Mock<IGenericRepository<Producto>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var movimiento = new MovimientoProducto
        {
            ProductoId = entity.Id,
            AlmacenId = Guid.NewGuid(),
            ClaveOrigen = "TEST",
            CreatedBy = "test",
        };
        var movimientoRepo = new Mock<IGenericRepository<MovimientoProducto>>();
        movimientoRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<MovimientoProducto, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(movimiento);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Producto>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.Repository<MovimientoProducto>()).Returns(movimientoRepo.Object);

        var handler = new DeleteProductoCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteProductoCommand(entity.Id), default);

        Assert.True(result.EsFallo);
        Assert.Equal("producto.conflicto", result.Errores[0].Codigo);
        repo.Verify(r => r.Remove(It.IsAny<Producto>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static IGenericRepository<MovimientoProducto> SinMovimientos()
    {
        var repo = new Mock<IGenericRepository<MovimientoProducto>>();
        repo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<MovimientoProducto, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MovimientoProducto?)null);
        return repo.Object;
    }
}
