using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.CuentasContables.Commands;
using OpenSource1.Application.Features.CuentasContables.Handlers;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;

namespace OpenSource1.SmokeTests.Features.CuentasContables.Handlers;

public class DeleteCuentaContableCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFallo_WhenCuentaDoesNotExist()
    {
        var repo = new Mock<IGenericRepository<CuentaContable>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((CuentaContable?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<CuentaContable>()).Returns(repo.Object);
        var usoService = new Mock<ICuentaContableUsoService>();

        var handler = new DeleteCuentaContableCommandHandler(unitOfWork.Object, usoService.Object);
        var result = await handler.Handle(new DeleteCuentaContableCommand(Guid.NewGuid()), default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Codigo == "cuenta_contable.no_encontrado");
    }

    [Fact]
    public async Task Handle_RemovesAndSaves_WhenCuentaExisteYSinUso()
    {
        var entity = new CuentaContable
        {
            Numero = "1102", Nombre = "Bancos", TipoCuenta = TipoCuentaContable.Posteo, TipoResultado = TipoResultadoCuenta.Balance
        };
        var repo = new Mock<IGenericRepository<CuentaContable>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<CuentaContable>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var usoService = new Mock<ICuentaContableUsoService>();
        usoService.Setup(s => s.EstaEnUsoAsync(entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var handler = new DeleteCuentaContableCommandHandler(unitOfWork.Object, usoService.Object);
        var result = await handler.Handle(new DeleteCuentaContableCommand(entity.Id), default);

        Assert.True(result.EsExito);
        repo.Verify(r => r.Remove(entity), Times.Once);
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsConflicto_WhenCuentaEstaEnUso()
    {
        var entity = new CuentaContable
        {
            Numero = "1102", Nombre = "Bancos", TipoCuenta = TipoCuentaContable.Posteo, TipoResultado = TipoResultadoCuenta.Balance
        };
        var repo = new Mock<IGenericRepository<CuentaContable>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<CuentaContable>()).Returns(repo.Object);
        var usoService = new Mock<ICuentaContableUsoService>();
        usoService.Setup(s => s.EstaEnUsoAsync(entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new DeleteCuentaContableCommandHandler(unitOfWork.Object, usoService.Object);
        var result = await handler.Handle(new DeleteCuentaContableCommand(entity.Id), default);

        Assert.True(result.EsFallo);
        Assert.Equal("cuenta_contable.conflicto", result.Errores[0].Codigo);
        repo.Verify(r => r.Remove(It.IsAny<CuentaContable>()), Times.Never);
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
