using System.Linq.Expressions;
using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Almacenes.Commands;
using OpenSource1.Application.Features.Almacenes.Handlers;
using OpenSource1.Core.Entities;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Features.Almacenes.Handlers;

public class CreateAlmacenCommandHandlerTests
{
    [Fact]
    public async Task Handle_AddsEntity_NormalizaCodigoYDevuelveRespuestaMapeada()
    {
        var almacenes = new RepositorioEnMemoria<Almacen>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Almacen>()).Returns(almacenes.Repo);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateAlmacenCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(
            new CreateAlmacenCommand(" pri2 ", " Sucursal ", " Calle 1 ", null, " Santiago ", " do ", false, false), default);

        Assert.True(result.EsExito);
        var agregado = Assert.Single(almacenes.Datos);
        Assert.Equal("PRI2", agregado.Codigo);
        Assert.Equal("Sucursal", agregado.Nombre);
        Assert.Equal("Calle 1", agregado.DireccionLinea1);
        Assert.Null(agregado.DireccionLinea2);
        Assert.Equal("Santiago", agregado.Ciudad);
        Assert.Equal("DO", agregado.PaisCodigo);
        Assert.False(agregado.EsPredeterminado);
        Assert.Equal("PRI2", result.Valor.Codigo);
        Assert.Equal(agregado.Id, result.Valor.Id);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("", "Nombre")]
    [InlineData("A B", "Nombre")]
    [InlineData("CODIGOMUYLARGO", "Nombre")]
    [InlineData("COD", "")]
    public async Task Handle_ReturnsFallo_WhenDatosInvalidos(string codigo, string nombre)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new CreateAlmacenCommandHandler(unitOfWork.Object);

        var result = await handler.Handle(
            new CreateAlmacenCommand(codigo, nombre, null, null, null, null, false, false), default);

        Assert.True(result.EsFallo);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CodigoInvalido_DevuelveErrorConCampoCodigo()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new CreateAlmacenCommandHandler(unitOfWork.Object);

        var result = await handler.Handle(
            new CreateAlmacenCommand("A B", "Nombre", null, null, null, null, false, false), default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Campo == "Codigo");
    }

    [Fact]
    public async Task Handle_PaisCodigoInvalido_DevuelveFallo()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new CreateAlmacenCommandHandler(unitOfWork.Object);

        var result = await handler.Handle(
            new CreateAlmacenCommand("COD", "Nombre", null, null, null, "ZZ", false, false), default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Campo == "PaisCodigo");
    }

    [Fact]
    public async Task Handle_ConEsPredeterminadoTrue_DesmarcaElAnteriorEnTransaccion()
    {
        var anterior = new Almacen { Codigo = "OLD", Nombre = "Anterior", EsPredeterminado = true };
        var almacenes = new RepositorioEnMemoria<Almacen>();
        almacenes.Agregar(anterior);

        var transaccion = new Mock<IAsyncDisposable>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Almacen>()).Returns(almacenes.Repo);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(transaccion.Object);

        var handler = new CreateAlmacenCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(
            new CreateAlmacenCommand("NEW", "Nueva", null, null, null, null, false, true), default);

        Assert.True(result.EsExito);
        Assert.False(anterior.EsPredeterminado);
        Assert.True(result.Valor.EsPredeterminado);
        unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        almacenes.Mock.Verify(r => r.Update(anterior), Times.Once);
    }

    [Fact]
    public async Task Handle_ConEsPredeterminadoTrue_YSinAnterior_NoDesmarcaNada()
    {
        var almacenes = new RepositorioEnMemoria<Almacen>();
        var transaccion = new Mock<IAsyncDisposable>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<Almacen>()).Returns(almacenes.Repo);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(transaccion.Object);

        var handler = new CreateAlmacenCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(
            new CreateAlmacenCommand("NEW", "Nueva", null, null, null, null, false, true), default);

        Assert.True(result.EsExito);
        almacenes.Mock.Verify(r => r.Update(It.IsAny<Almacen>()), Times.Never);
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
