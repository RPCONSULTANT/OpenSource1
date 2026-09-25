using MediatR;
using Moq;
using OpenSource1.Application.Common.Behaviors;
using OpenSource1.Application.Common.Messaging;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Core.Common;

namespace OpenSource1.SmokeTests.Application.Behaviors;

public class TransactionBehaviorTests
{
    private sealed record FakeCommand(string Nombre) : ICommand<Result>;

    private sealed record FakeQuery(string Nombre) : IQuery<Result>;

    private sealed class FakeAmbito : IAsyncDisposable
    {
        public bool Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task Handle_ConICommand_YResultadoExitoso_AbreYConfirmaTransaccion()
    {
        var ambito = new FakeAmbito();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.HayTransaccionActiva).Returns(false);
        unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ambito);

        var behavior = new TransactionBehavior<FakeCommand, Result>(unitOfWork.Object);

        var respuesta = await behavior.Handle(
            new FakeCommand("x"), _ => Task.FromResult(Result.Exito()), CancellationToken.None);

        Assert.True(respuesta.EsExito);
        unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.True(ambito.Disposed);
    }

    [Fact]
    public async Task Handle_ConICommand_YResultadoFallido_DeshaceLaTransaccion()
    {
        var ambito = new FakeAmbito();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.HayTransaccionActiva).Returns(false);
        unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ambito);

        var behavior = new TransactionBehavior<FakeCommand, Result>(unitOfWork.Object);

        var fallo = Result.Fallo(new Error("cliente.no_encontrado", "No existe.", null));
        var respuesta = await behavior.Handle(new FakeCommand("x"), _ => Task.FromResult(fallo), CancellationToken.None);

        Assert.True(respuesta.EsFallo);
        unitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ConIQuery_NoAbreTransaccion()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.HayTransaccionActiva).Returns(false);

        var behavior = new TransactionBehavior<FakeQuery, Result>(unitOfWork.Object);

        var handlerInvocado = false;
        var respuesta = await behavior.Handle(new FakeQuery("x"), _ =>
        {
            handlerInvocado = true;
            return Task.FromResult(Result.Exito());
        }, CancellationToken.None);

        Assert.True(handlerInvocado);
        Assert.True(respuesta.EsExito);
        unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ConICommand_PeroYaHayTransaccionActiva_NoAbreOtra()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.HayTransaccionActiva).Returns(true);

        var behavior = new TransactionBehavior<FakeCommand, Result>(unitOfWork.Object);

        var respuesta = await behavior.Handle(
            new FakeCommand("x"), _ => Task.FromResult(Result.Exito()), CancellationToken.None);

        Assert.True(respuesta.EsExito);
        unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
