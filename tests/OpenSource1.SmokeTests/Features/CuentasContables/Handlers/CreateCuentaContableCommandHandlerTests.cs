using Moq;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.CuentasContables.Commands;
using OpenSource1.Application.Features.CuentasContables.Handlers;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Features.CuentasContables.Handlers;

public class CreateCuentaContableCommandHandlerTests
{
    [Fact]
    public async Task Handle_AddsEntity_NormalizaNumeroYDevuelveRespuestaMapeada()
    {
        var cuentas = new RepositorioEnMemoria<CuentaContable>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<CuentaContable>()).Returns(cuentas.Repo);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateCuentaContableCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(
            new CreateCuentaContableCommand(
                " 1102 ", " Bancos ", TipoCuentaContable.Posteo, TipoResultadoCuenta.Balance, true, false, 1),
            default);

        Assert.True(result.EsExito);
        var agregado = Assert.Single(cuentas.Datos);
        Assert.Equal("1102", agregado.Numero);
        Assert.Equal("Bancos", agregado.Nombre);
        Assert.Equal(TipoCuentaContable.Posteo, agregado.TipoCuenta);
        Assert.Equal(TipoResultadoCuenta.Balance, agregado.TipoResultado);
        Assert.True(agregado.PosteoDirecto);
        Assert.False(agregado.Bloqueada);
        Assert.Equal(1, agregado.Sangria);
        Assert.Equal("1102", result.Valor.Numero);
        Assert.Equal(agregado.Id, result.Valor.Id);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("", "Nombre")]
    [InlineData("AB01", "Nombre")]
    [InlineData("12345678901234567890X", "Nombre")]
    [InlineData("1102", "")]
    public async Task Handle_ReturnsFallo_WhenDatosInvalidos(string numero, string nombre)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new CreateCuentaContableCommandHandler(unitOfWork.Object);

        var result = await handler.Handle(
            new CreateCuentaContableCommand(numero, nombre, TipoCuentaContable.Posteo, TipoResultadoCuenta.Balance, false, false, 0),
            default);

        Assert.True(result.EsFallo);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NumeroInvalido_DevuelveErrorConCampoNumero()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new CreateCuentaContableCommandHandler(unitOfWork.Object);

        var result = await handler.Handle(
            new CreateCuentaContableCommand("AB01", "Nombre", TipoCuentaContable.Posteo, TipoResultadoCuenta.Balance, false, false, 0),
            default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Campo == "Numero");
    }

    [Fact]
    public async Task Handle_SangriaFueraDeRango_DevuelveFalloConCampoSangria()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new CreateCuentaContableCommandHandler(unitOfWork.Object);

        var result = await handler.Handle(
            new CreateCuentaContableCommand("1102", "Bancos", TipoCuentaContable.Posteo, TipoResultadoCuenta.Balance, false, false, 11),
            default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Campo == "Sangria");
    }

    [Fact]
    public async Task Handle_TipoCuentaInvalido_DevuelveFalloConCampoTipoCuenta()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new CreateCuentaContableCommandHandler(unitOfWork.Object);

        var result = await handler.Handle(
            new CreateCuentaContableCommand("1102", "Bancos", (TipoCuentaContable)99, TipoResultadoCuenta.Balance, false, false, 0),
            default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Campo == "TipoCuenta");
    }
}
