using System.Reflection;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Common.Behaviors;

/// <summary>
/// Ejecuta todos los <see cref="IValidator{T}"/> registrados para <typeparamref name="TRequest"/>
/// antes de invocar el handler. Si hay fallos, el handler nunca se llama.
///
/// Cuando <typeparamref name="TResponse"/> es <see cref="Result"/> o <see cref="Result{T}"/>, la
/// falla se traduce a un resultado fallido (construido por reflexión, porque el tipo concreto de
/// <c>Result&lt;T&gt;</c> varía por request y no hay forma genérica de invocar
/// <c>Result&lt;T&gt;.Fallo</c> sin conocer T en tiempo de compilación). Para cualquier otro
/// <typeparamref name="TResponse"/> -por ejemplo los flujos de auth actuales, que no devuelven
/// Result- se lanza <see cref="ValidationException"/>, el comportamiento estándar de
/// FluentValidation, preservando compatibilidad con esos validadores.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next(cancellationToken);
        }

        var contexto = new ValidationContext<TRequest>(request);
        var fallos = new List<ValidationFailure>();

        foreach (var validador in validators)
        {
            var resultado = await validador.ValidateAsync(contexto, cancellationToken);
            fallos.AddRange(resultado.Errors);
        }

        if (fallos.Count == 0)
        {
            return await next(cancellationToken);
        }

        return ConstruirRespuestaFallida(fallos);
    }

    private static TResponse ConstruirRespuestaFallida(List<ValidationFailure> fallos)
    {
        var errores = fallos
            .Select(f => new Error(
                string.IsNullOrWhiteSpace(f.ErrorCode) ? "validacion.campo_invalido" : f.ErrorCode,
                f.ErrorMessage,
                f.PropertyName))
            .ToArray();

        var tipoRespuesta = typeof(TResponse);

        if (tipoRespuesta == typeof(Result))
        {
            return (TResponse)(object)Result.Fallo(errores);
        }

        if (tipoRespuesta.IsGenericType && tipoRespuesta.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var metodoFallo = tipoRespuesta.GetMethod(
                "Fallo",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly,
                null,
                [typeof(Error[])],
                null)
                ?? throw new InvalidOperationException(
                    $"No se encontró {tipoRespuesta.Name}.Fallo(Error[]) por reflexión.");

            return (TResponse)metodoFallo.Invoke(null, [errores])!;
        }

        throw new ValidationException(fallos);
    }
}
