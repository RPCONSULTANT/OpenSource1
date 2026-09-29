using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.UnidadesMedida.Commands;
using OpenSource1.Application.Features.UnidadesMedida.Handlers;
using OpenSource1.Core.Entities;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Features.UnidadesMedida.Handlers;

public class UpdateUnidadMedidaCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFallo_WhenUnidadDoesNotExist()
    {
        var repo = new Mock<IGenericRepository<UnidadMedida>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((UnidadMedida?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<UnidadMedida>()).Returns(repo.Object);

        var handler = new UpdateUnidadMedidaCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new UpdateUnidadMedidaCommand(Guid.NewGuid(), "COD", "Nombre", 1), default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Codigo == "unidad_medida.no_encontrado");
    }

    [Fact]
    public async Task Handle_UpdatesAndSaves_WhenUnidadExists()
    {
        var entity = new UnidadMedida { Codigo = "OLD", Nombre = "Vieja", Decimales = 0 };
        var repo = new Mock<IGenericRepository<UnidadMedida>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<UnidadMedida>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.Repository<Producto>()).Returns(new RepositorioEnMemoria<Producto>().Repo);
        unitOfWork.Setup(u => u.Repository<UnidadMedidaProducto>()).Returns(new RepositorioEnMemoria<UnidadMedidaProducto>().Repo);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateUnidadMedidaCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new UpdateUnidadMedidaCommand(entity.Id, " new ", " Nueva ", 3), default);

        Assert.True(result.EsExito);
        Assert.Equal("NEW", entity.Codigo);
        Assert.Equal("Nueva", entity.Nombre);
        Assert.Equal(3, entity.Decimales);
        Assert.Equal("NEW", result.Valor.Codigo);
        repo.Verify(r => r.Update(entity), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsFallo_WhenDatosInvalidos()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new UpdateUnidadMedidaCommandHandler(unitOfWork.Object);

        var result = await handler.Handle(new UpdateUnidadMedidaCommand(Guid.NewGuid(), "", "", 9), default);

        Assert.True(result.EsFallo);
        Assert.Equal(3, result.Errores.Count);
        unitOfWork.Verify(u => u.Repository<UnidadMedida>(), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsConflicto_WhenSeCambiaElCodigoDeUnaUnidadQueEsBaseDeUnProducto()
    {
        var entity = new UnidadMedida { Codigo = "OLD", Nombre = "Vieja", Decimales = 0 };
        var (unitOfWork, repo) = Preparar(entity, productoConEstaUnidadComoBase: true);

        var handler = new UpdateUnidadMedidaCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new UpdateUnidadMedidaCommand(entity.Id, "NEW", "Vieja", 0), default);

        Assert.True(result.EsFallo);
        Assert.Equal("unidad_medida.en_uso.conflicto", result.Errores[0].Codigo);
        Assert.Equal("Codigo", result.Errores[0].Campo);
        Assert.Equal("OLD", entity.Codigo);
        repo.Verify(r => r.Update(It.IsAny<UnidadMedida>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PermiteCambiarElNombreDeUnaUnidadEnUsoSiElCodigoNoCambia()
    {
        var entity = new UnidadMedida { Codigo = "OLD", Nombre = "Vieja", Decimales = 0 };
        var (unitOfWork, _) = Preparar(entity, productoConEstaUnidadComoBase: true);

        var handler = new UpdateUnidadMedidaCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new UpdateUnidadMedidaCommand(entity.Id, " old ", "Renombrada", 0), default);

        Assert.True(result.EsExito);
        Assert.Equal("Renombrada", entity.Nombre);
    }

    private static (Mock<IUnitOfWork> UnitOfWork, Mock<IGenericRepository<UnidadMedida>> Repo) Preparar(UnidadMedida entity, bool productoConEstaUnidadComoBase)
    {
        var repo = new Mock<IGenericRepository<UnidadMedida>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var productos = new RepositorioEnMemoria<Producto>();
        if (productoConEstaUnidadComoBase)
        {
            productos.Agregar(new Producto { Codigo = "P1", Nombre = "P", UnidadMedidaBaseId = entity.Id, CategoriaId = Guid.NewGuid() });
        }

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<UnidadMedida>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.Repository<Producto>()).Returns(productos.Repo);
        unitOfWork.Setup(u => u.Repository<UnidadMedidaProducto>()).Returns(new RepositorioEnMemoria<UnidadMedidaProducto>().Repo);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return (unitOfWork, repo);
    }
}
