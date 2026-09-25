using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Almacenes.Commands;
using OpenSource1.Application.Features.Almacenes.Handlers;
using OpenSource1.Core.Entities;

namespace OpenSource1.SmokeTests.Features.Almacenes.Handlers;

public class DeleteAlmacenCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFallo_WhenAlmacenDoesNotExist()
    {
        var repo = new Mock<IGenericRepository<Almacen>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((Almacen?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Almacen>()).Returns(repo.Object);

        var handler = new DeleteAlmacenCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteAlmacenCommand(Guid.NewGuid()), default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Codigo == "almacen.no_encontrado");
    }

    [Fact]
    public async Task Handle_RemovesAndSaves_WhenAlmacenExistsAndNoEsPredeterminado()
    {
        var entity = new Almacen { Codigo = "COD", Nombre = "Test", EsPredeterminado = false };
        var repo = new Mock<IGenericRepository<Almacen>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Almacen>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new DeleteAlmacenCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteAlmacenCommand(entity.Id), default);

        Assert.True(result.EsExito);
        repo.Verify(r => r.Remove(entity), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsConflicto_WhenAlmacenEsElPredeterminado()
    {
        var entity = new Almacen { Codigo = "COD", Nombre = "Test", EsPredeterminado = true };
        var repo = new Mock<IGenericRepository<Almacen>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Almacen>()).Returns(repo.Object);

        var handler = new DeleteAlmacenCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteAlmacenCommand(entity.Id), default);

        Assert.True(result.EsFallo);
        Assert.Equal("almacen.conflicto", result.Errores[0].Codigo);
        repo.Verify(r => r.Remove(It.IsAny<Almacen>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
