using Moq;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.CuentasContables.Commands;
using OpenSource1.Application.Features.CuentasContables.Handlers;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Features.CuentasContables.Handlers;

public class UpdateCuentaContableCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFallo_WhenCuentaDoesNotExist()
    {
        var cuentas = new RepositorioEnMemoria<CuentaContable>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<CuentaContable>()).Returns(cuentas.Repo);
        var usoService = new Mock<ICuentaContableUsoService>();

        var handler = new UpdateCuentaContableCommandHandler(unitOfWork.Object, usoService.Object);
        var result = await handler.Handle(
            new UpdateCuentaContableCommand(
                Guid.NewGuid(), "1102", "Bancos", TipoCuentaContable.Posteo, TipoResultadoCuenta.Balance, null, null, null, 1),
            default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Codigo == "cuenta_contable.no_encontrado");
    }

    [Fact]
    public async Task Handle_ActualizaNumeroYNombre_YConservaCamposNoInformados()
    {
        var entity = new CuentaContable
        {
            Numero = "1102", Nombre = "Bancos", TipoCuenta = TipoCuentaContable.Posteo,
            TipoResultado = TipoResultadoCuenta.Balance, PosteoDirecto = true, Bloqueada = false, Sangria = 1
        };
        var cuentas = new RepositorioEnMemoria<CuentaContable>();
        cuentas.Agregar(entity);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<CuentaContable>()).Returns(cuentas.Repo);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var usoService = new Mock<ICuentaContableUsoService>();

        var handler = new UpdateCuentaContableCommandHandler(unitOfWork.Object, usoService.Object);
        var result = await handler.Handle(
            new UpdateCuentaContableCommand(
                entity.Id, " 1103 ", " Bancos renombrado ", TipoCuentaContable.Posteo, TipoResultadoCuenta.Balance,
                null, null, null, 1),
            default);

        Assert.True(result.EsExito);
        Assert.Equal("1103", entity.Numero);
        Assert.Equal("Bancos renombrado", entity.Nombre);
        // No informados: se conservan.
        Assert.True(entity.PosteoDirecto);
        Assert.False(entity.Bloqueada);
        Assert.Equal(1, entity.Sangria);
        cuentas.Mock.Verify(r => r.Update(entity), Times.Once);
        cuentas.Mock.Verify(r => r.EstablecerVersionOriginal(entity, 1), Times.Once);
    }

    [Fact]
    public async Task Handle_DatosInvalidos_DevuelveFalloSinGuardar()
    {
        var entity = new CuentaContable
        {
            Numero = "1102", Nombre = "Bancos", TipoCuenta = TipoCuentaContable.Posteo, TipoResultado = TipoResultadoCuenta.Balance
        };
        var cuentas = new RepositorioEnMemoria<CuentaContable>();
        cuentas.Agregar(entity);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<CuentaContable>()).Returns(cuentas.Repo);
        var usoService = new Mock<ICuentaContableUsoService>();

        var handler = new UpdateCuentaContableCommandHandler(unitOfWork.Object, usoService.Object);
        var result = await handler.Handle(
            new UpdateCuentaContableCommand(
                entity.Id, "AB01", "Bancos", TipoCuentaContable.Posteo, TipoResultadoCuenta.Balance, null, null, null, 1),
            default);

        Assert.True(result.EsFallo);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CambiaDePosteoAOtroTipo_YEstaEnUso_DevuelveConflicto()
    {
        var entity = new CuentaContable
        {
            Numero = "1102", Nombre = "Bancos", TipoCuenta = TipoCuentaContable.Posteo, TipoResultado = TipoResultadoCuenta.Balance
        };
        var cuentas = new RepositorioEnMemoria<CuentaContable>();
        cuentas.Agregar(entity);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<CuentaContable>()).Returns(cuentas.Repo);
        var usoService = new Mock<ICuentaContableUsoService>();
        usoService.Setup(s => s.EstaEnUsoAsync(entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new UpdateCuentaContableCommandHandler(unitOfWork.Object, usoService.Object);
        var result = await handler.Handle(
            new UpdateCuentaContableCommand(
                entity.Id, "1102", "Bancos", TipoCuentaContable.Total, TipoResultadoCuenta.Balance, null, null, null, 1),
            default);

        Assert.True(result.EsFallo);
        Assert.Equal("cuenta_contable.conflicto", result.Errores[0].Codigo);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        // El tipo original no cambió.
        Assert.Equal(TipoCuentaContable.Posteo, entity.TipoCuenta);
    }

    [Fact]
    public async Task Handle_CambiaDePosteoAOtroTipo_SinUso_SePermite()
    {
        var entity = new CuentaContable
        {
            Numero = "1102", Nombre = "Bancos", TipoCuenta = TipoCuentaContable.Posteo, TipoResultado = TipoResultadoCuenta.Balance
        };
        var cuentas = new RepositorioEnMemoria<CuentaContable>();
        cuentas.Agregar(entity);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<CuentaContable>()).Returns(cuentas.Repo);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var usoService = new Mock<ICuentaContableUsoService>();
        usoService.Setup(s => s.EstaEnUsoAsync(entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var handler = new UpdateCuentaContableCommandHandler(unitOfWork.Object, usoService.Object);
        var result = await handler.Handle(
            new UpdateCuentaContableCommand(
                entity.Id, "1102", "Bancos", TipoCuentaContable.Total, TipoResultadoCuenta.Balance, null, null, null, 1),
            default);

        Assert.True(result.EsExito);
        Assert.Equal(TipoCuentaContable.Total, entity.TipoCuenta);
    }

    [Fact]
    public async Task Handle_CambiaEntreTiposDistintosDePosteo_NoConsultaLaGuarda()
    {
        // La guarda solo se consulta cuando SALE de Posteo: cambiar entre Encabezado y Total, o
        // entrar A Posteo, no tiene por qué chocar con nada todavía.
        var entity = new CuentaContable
        {
            Numero = "1", Nombre = "Activos", TipoCuenta = TipoCuentaContable.Encabezado, TipoResultado = TipoResultadoCuenta.Balance
        };
        var cuentas = new RepositorioEnMemoria<CuentaContable>();
        cuentas.Agregar(entity);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<CuentaContable>()).Returns(cuentas.Repo);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var usoService = new Mock<ICuentaContableUsoService>();

        var handler = new UpdateCuentaContableCommandHandler(unitOfWork.Object, usoService.Object);
        var result = await handler.Handle(
            new UpdateCuentaContableCommand(
                entity.Id, "1", "Activos", TipoCuentaContable.Total, TipoResultadoCuenta.Balance, null, null, null, 1),
            default);

        Assert.True(result.EsExito);
        usoService.Verify(s => s.EstaEnUsoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
