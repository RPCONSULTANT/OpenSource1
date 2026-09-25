using Moq;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Almacenes.Commands;
using OpenSource1.Application.Features.Almacenes.Handlers;
using OpenSource1.Core.Entities;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Features.Almacenes.Handlers;

public class UpdateAlmacenCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFallo_WhenAlmacenDoesNotExist()
    {
        var almacenes = new RepositorioEnMemoria<Almacen>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Almacen>()).Returns(almacenes.Repo);

        var handler = new UpdateAlmacenCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(
            new UpdateAlmacenCommand(Guid.NewGuid(), "COD", "Nombre", null, null, null, null, null, null), default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Codigo == "almacen.no_encontrado");
    }

    [Fact]
    public async Task Handle_ActualizaCodigoYNombre_YConservaCamposNoInformados()
    {
        var entity = new Almacen
        {
            Codigo = "OLD", Nombre = "Vieja", DireccionLinea1 = "Calle vieja", Ciudad = "Santiago", PaisCodigo = "DO"
        };
        var almacenes = new RepositorioEnMemoria<Almacen>();
        almacenes.Agregar(entity);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Almacen>()).Returns(almacenes.Repo);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateAlmacenCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(
            new UpdateAlmacenCommand(entity.Id, " new ", " Nueva ", null, null, null, null, null, null), default);

        Assert.True(result.EsExito);
        Assert.Equal("NEW", entity.Codigo);
        Assert.Equal("Nueva", entity.Nombre);
        // No informados: se conservan.
        Assert.Equal("Calle vieja", entity.DireccionLinea1);
        Assert.Equal("Santiago", entity.Ciudad);
        Assert.Equal("DO", entity.PaisCodigo);
        almacenes.Mock.Verify(r => r.Update(entity), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CadenaVacia_LimpiaUnCampoDeDireccionOpcional()
    {
        var entity = new Almacen { Codigo = "OLD", Nombre = "Vieja", Ciudad = "Santiago" };
        var almacenes = new RepositorioEnMemoria<Almacen>();
        almacenes.Agregar(entity);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Almacen>()).Returns(almacenes.Repo);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateAlmacenCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(
            new UpdateAlmacenCommand(entity.Id, "OLD", "Vieja", null, null, "", null, null, null), default);

        Assert.True(result.EsExito);
        Assert.Null(entity.Ciudad);
    }

    [Fact]
    public async Task Handle_DatosInvalidos_DevuelveFalloSinGuardar()
    {
        var entity = new Almacen { Codigo = "OLD", Nombre = "Vieja" };
        var almacenes = new RepositorioEnMemoria<Almacen>();
        almacenes.Agregar(entity);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Almacen>()).Returns(almacenes.Repo);

        var handler = new UpdateAlmacenCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(
            new UpdateAlmacenCommand(entity.Id, "A B", "Vieja", null, null, null, null, null, null), default);

        Assert.True(result.EsFallo);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EsPredeterminadoFalse_SobreElPredeterminadoActual_DevuelvePredeterminadoRequerido()
    {
        var entity = new Almacen { Codigo = "OLD", Nombre = "Vieja", EsPredeterminado = true };
        var almacenes = new RepositorioEnMemoria<Almacen>();
        almacenes.Agregar(entity);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Almacen>()).Returns(almacenes.Repo);

        var handler = new UpdateAlmacenCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(
            new UpdateAlmacenCommand(entity.Id, "OLD", "Vieja", null, null, null, null, null, false), default);

        Assert.True(result.EsFallo);
        Assert.Equal("almacen.predeterminado_requerido", result.Errores[0].Codigo);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.True(entity.EsPredeterminado);
    }

    [Fact]
    public async Task Handle_EsPredeterminadoTrue_SobreUnoQueNoLoEs_DesmarcaElAnteriorEnTransaccion()
    {
        var anterior = new Almacen { Codigo = "OLD", Nombre = "Anterior", EsPredeterminado = true };
        var entity = new Almacen { Codigo = "NEW", Nombre = "Nueva", EsPredeterminado = false };
        var almacenes = new RepositorioEnMemoria<Almacen>();
        almacenes.Agregar(anterior);
        almacenes.Agregar(entity);

        var transaccion = new Mock<IAsyncDisposable>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Almacen>()).Returns(almacenes.Repo);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(transaccion.Object);

        var handler = new UpdateAlmacenCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(
            new UpdateAlmacenCommand(entity.Id, "NEW", "Nueva", null, null, null, null, null, true), default);

        Assert.True(result.EsExito);
        Assert.False(anterior.EsPredeterminado);
        Assert.True(entity.EsPredeterminado);
        unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EsPredeterminadoTrue_SobreElQueYaLoEs_NoAbreTransaccionNiTocaOtros()
    {
        var entity = new Almacen { Codigo = "OLD", Nombre = "Vieja", EsPredeterminado = true };
        var almacenes = new RepositorioEnMemoria<Almacen>();
        almacenes.Agregar(entity);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Almacen>()).Returns(almacenes.Repo);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateAlmacenCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(
            new UpdateAlmacenCommand(entity.Id, "OLD", "Vieja", null, null, null, null, null, true), default);

        Assert.True(result.EsExito);
        Assert.True(entity.EsPredeterminado);
        unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
