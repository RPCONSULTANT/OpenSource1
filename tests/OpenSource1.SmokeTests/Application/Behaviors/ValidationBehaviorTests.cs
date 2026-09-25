using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Moq;
using OpenSource1.Application.Common.Behaviors;
using OpenSource1.Core.Common;

namespace OpenSource1.SmokeTests.Application.Behaviors;

public class ValidationBehaviorTests
{
    // Públicos: Moq necesita generar un proxy de IValidator<TRequest>, y Castle DynamicProxy no
    // puede crear un proxy para un tipo genérico cerrado sobre un tipo anidado no público.
    public sealed record FakeRequest(string Nombre) : IRequest<Result>;

    public sealed record FakeRequestConValor(string Nombre) : IRequest<Result<string>>;

    [Fact]
    public async Task Handle_ConValidadorQueFalla_NoInvocaHandler_YDevuelveResultadoFallido()
    {
        var fallo = new ValidationFailure("Nombre", "El nombre es obligatorio.") { ErrorCode = "cliente.nombre_requerido" };
        var validador = new Mock<IValidator<FakeRequest>>();
        validador
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<FakeRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([fallo]));

        var behavior = new ValidationBehavior<FakeRequest, Result>([validador.Object]);

        var handlerInvocado = false;
        Task<Result> Next(CancellationToken _)
        {
            handlerInvocado = true;
            return Task.FromResult(Result.Exito());
        }

        var respuesta = await behavior.Handle(new FakeRequest("x"), Next, CancellationToken.None);

        Assert.False(handlerInvocado);
        Assert.True(respuesta.EsFallo);
        Assert.Single(respuesta.Errores);
        Assert.Equal("cliente.nombre_requerido", respuesta.Errores[0].Codigo);
        Assert.Equal("Nombre", respuesta.Errores[0].Campo);
        Assert.Equal("El nombre es obligatorio.", respuesta.Errores[0].Mensaje);
    }

    [Fact]
    public async Task Handle_ConValidadorQueFalla_YResponseGenerico_DevuelveResultadoDeTFallido()
    {
        // Sin ErrorCode explícito: el behavior debe caer en el código por defecto.
        var fallo = new ValidationFailure("Nombre", "Requerido");
        var validador = new Mock<IValidator<FakeRequestConValor>>();
        validador
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<FakeRequestConValor>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([fallo]));

        var behavior = new ValidationBehavior<FakeRequestConValor, Result<string>>([validador.Object]);

        var respuesta = await behavior.Handle(
            new FakeRequestConValor("x"),
            _ => throw new InvalidOperationException("El handler no debía invocarse."),
            CancellationToken.None);

        Assert.True(respuesta.EsFallo);
        Assert.Equal("validacion.campo_invalido", respuesta.Errores[0].Codigo);
        Assert.Equal("Nombre", respuesta.Errores[0].Campo);
        Assert.Throws<InvalidOperationException>(() => respuesta.Valor);
    }

    [Fact]
    public async Task Handle_SinValidadoresRegistrados_InvocaHandler()
    {
        var behavior = new ValidationBehavior<FakeRequest, Result>([]);

        var respuesta = await behavior.Handle(
            new FakeRequest("x"), _ => Task.FromResult(Result.Exito()), CancellationToken.None);

        Assert.True(respuesta.EsExito);
    }

    [Fact]
    public async Task Handle_ConValidadoresQuePasan_InvocaHandler()
    {
        var validador = new Mock<IValidator<FakeRequest>>();
        validador
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<FakeRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var behavior = new ValidationBehavior<FakeRequest, Result>([validador.Object]);

        var handlerInvocado = false;
        Task<Result> Next(CancellationToken _)
        {
            handlerInvocado = true;
            return Task.FromResult(Result.Exito());
        }

        var respuesta = await behavior.Handle(new FakeRequest("x"), Next, CancellationToken.None);

        Assert.True(handlerInvocado);
        Assert.True(respuesta.EsExito);
    }
}
