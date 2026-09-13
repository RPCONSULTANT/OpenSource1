using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenSource1.Core.Common;

namespace OpenSource1.Api.Infrastructure;

/// <summary>
/// Único punto de captura para excepciones no manejadas por el pipeline. Sin este handler
/// (y sin <c>app.UseExceptionHandler()</c> enchufado en Program.cs) <c>AddProblemDetails()</c>
/// no produce nada: en Development el resultado es una página HTML con stack trace, y fuera
/// de Development un 500 desnudo sin cuerpo.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is ErroresDeDominioException dominio)
        {
            logger.LogWarning(
                exception,
                "Error de validación de dominio no capturado en la capa correspondiente.");

            var problema = new ValidationProblemDetails(
                dominio.Errores
                    .GroupBy(e => e.Campo ?? string.Empty)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Mensaje).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Los datos enviados no son válidos.",
            };

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = problema,
            });
        }

        if (exception is DbUpdateConcurrencyException concurrencia)
        {
            logger.LogWarning(
                concurrencia,
                "Conflicto de concurrencia optimista: el registro fue modificado por otro usuario.");

            var problemaConcurrencia = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "El registro fue modificado por otro usuario.",
            };

            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = problemaConcurrencia,
            });
        }

        logger.LogError(exception, "Excepción no controlada.");

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var problemaGenerico = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Error interno del servidor.",
            Detail = environment.IsDevelopment() ? exception.Message : null,
        };
        problemaGenerico.Extensions["traceId"] = httpContext.TraceIdentifier;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemaGenerico,
        });
    }
}
