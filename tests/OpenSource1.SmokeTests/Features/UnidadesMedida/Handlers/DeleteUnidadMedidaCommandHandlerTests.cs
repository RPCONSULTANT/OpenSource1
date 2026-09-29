using System.Linq.Expressions;
using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.UnidadesMedida.Commands;
using OpenSource1.Application.Features.UnidadesMedida.Handlers;
using OpenSource1.Core.Entities;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Features.UnidadesMedida.Handlers;

public class DeleteUnidadMedidaCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFallo_WhenUnidadDoesNotExist()
    {
        var repo = new Mock<IGenericRepository<UnidadMedida>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((UnidadMedida?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<UnidadMedida>()).Returns(repo.Object);

        var handler = new DeleteUnidadMedidaCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteUnidadMedidaCommand(Guid.NewGuid()), default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Codigo == "unidad_medida.no_encontrado");
    }

    [Fact]
    public async Task Handle_RemovesAndSaves_WhenUnidadExistsAndIsNotInUse()
    {
        var (unitOfWork, repo, entity) = Preparar(enUso: false);

        var handler = new DeleteUnidadMedidaCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteUnidadMedidaCommand(entity.Id), default);

        Assert.True(result.EsExito);
        repo.Verify(r => r.Remove(entity), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsConflicto_WhenUnidadEsLaBaseDeUnProducto()
    {
        var (unitOfWork, repo, entity) = Preparar(enUso: false);
        var productos = new RepositorioEnMemoria<Producto>();
        productos.Agregar(new Producto { Codigo = "P1", Nombre = "P", UnidadMedidaBaseId = entity.Id, CategoriaId = Guid.NewGuid() });
        unitOfWork.Setup(u => u.Repository<Producto>()).Returns(productos.Repo);

        var handler = new DeleteUnidadMedidaCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteUnidadMedidaCommand(entity.Id), default);

        Assert.True(result.EsFallo);
        Assert.Equal("unidad_medida.en_uso.conflicto", result.Errores[0].Codigo);
        repo.Verify(r => r.Remove(It.IsAny<UnidadMedida>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsConflicto_WhenUnidadEstaAsociadaAUnProducto()
    {
        var (unitOfWork, repo, entity) = Preparar(enUso: true);

        var handler = new DeleteUnidadMedidaCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteUnidadMedidaCommand(entity.Id), default);

        Assert.True(result.EsFallo);
        Assert.EndsWith(".conflicto", result.Errores[0].Codigo);
        repo.Verify(r => r.Remove(It.IsAny<UnidadMedida>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (Mock<IUnitOfWork> UnitOfWork, Mock<IGenericRepository<UnidadMedida>> Repo, UnidadMedida Entity) Preparar(bool enUso)
    {
        var entity = new UnidadMedida { Codigo = "COD", Nombre = "Test", Decimales = 0 };
        var repo = new Mock<IGenericRepository<UnidadMedida>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var repoAsociaciones = new Mock<IGenericRepository<UnidadMedidaProducto>>();
        repoAsociaciones
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnidadMedidaProducto, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(enUso ? new UnidadMedidaProducto { ProductoId = Guid.NewGuid(), UnidadMedidaId = entity.Id, CantidadPorUnidadMedida = 1m } : null);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<UnidadMedida>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.Repository<UnidadMedidaProducto>()).Returns(repoAsociaciones.Object);
        unitOfWork.Setup(u => u.Repository<Producto>()).Returns(new RepositorioEnMemoria<Producto>().Repo);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        return (unitOfWork, repo, entity);
    }
}
