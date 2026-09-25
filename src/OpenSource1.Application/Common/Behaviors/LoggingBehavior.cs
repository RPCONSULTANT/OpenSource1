using MediatR;
using Microsoft.Extensions.Logging;

namespace OpenSource1.Application.Common.Behaviors;

/// <summary>
/// Envuelve toda la cadena de behaviors: registra el inicio, el fin (éxito o excepción) y el
/// tiempo transcurrido de cada request de MediatR. Va primero en el pipeline para que el tiempo
/// medido incluya validación y transacción.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var nombreRequest = typeof(TRequest).Name;
        var cronometro = System.Diagnostics.Stopwatch.StartNew();

        logger.LogInformation("Procesando {Request}", nombreRequest);

        try
        {
            var respuesta = await next(cancellationToken);
            logger.LogInformation(
                "Procesado {Request} en {ElapsedMilliseconds}ms", nombreRequest, cronometro.ElapsedMilliseconds);
            return respuesta;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, "Error procesando {Request} tras {ElapsedMilliseconds}ms", nombreRequest, cronometro.ElapsedMilliseconds);
            throw;
        }
    }
}
