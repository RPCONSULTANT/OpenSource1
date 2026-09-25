using Microsoft.AspNetCore.Mvc;
using OpenSource1.Core.Common;

namespace OpenSource1.Api.Infrastructure;

/// <summary>
/// Puente entre <see cref="Result"/>/<see cref="Result{T}"/> y las respuestas HTTP de la API.
/// Hoy la consumen los controllers cuyos handlers ya devuelven <c>Result</c>/<c>Result&lt;T&gt;</c>
/// (por ejemplo <c>AppSettingsController</c>, <c>ClientesController</c>, <c>EntradasController</c>,
/// <c>ProductosController</c>, en sus acciones de listado). Los comandos de Cliente/Producto
/// siguen sin migrar a <c>ICommand&lt;T&gt;</c>/<c>IQuery&lt;T&gt;</c> a propósito (fase
/// posterior, tras el renombrado a SocioDeNegocio). Traduce solo el camino de fallo — el de éxito
/// depende de cada endpoint (código de estado, forma del cuerpo, Location, etc.) y se sigue
/// construyendo a mano en el controller.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Convierte un <see cref="Result"/> fallido en un <see cref="ValidationProblemDetails"/>
    /// agrupado por <see cref="Error.Campo"/>. El código de estado se decide por el sufijo del
    /// código del primer error: <c>*.no_encontrado</c> → 404, <c>*.conflicto</c> → 409,
    /// <c>*.bloqueado</c> → 422, cualquier otro → 400.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// El resultado es exitoso. Llamar a esta extensión solo tiene sentido tras comprobar
    /// <see cref="Result.EsFallo"/>; no hay una respuesta HTTP genérica para un éxito porque su
    /// forma depende del endpoint.
    /// </exception>
    public static IActionResult ToActionResult(this Result resultado, string titulo = "Los datos enviados no son válidos.")
    {
        ArgumentNullException.ThrowIfNull(resultado);

        if (resultado.EsExito)
        {
            throw new InvalidOperationException(
                "ToActionResult solo aplica a resultados fallidos; compruebe EsFallo antes de llamarla.");
        }

        var status = DeterminarStatus(resultado.Errores[0].Codigo);

        var problema = new ValidationProblemDetails(
            resultado.Errores
                .GroupBy(e => e.Campo ?? string.Empty)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Mensaje).ToArray()))
        {
            Status = status,
            Title = titulo,
        };

        return new ObjectResult(problema) { StatusCode = status };
    }

    private static int DeterminarStatus(string codigo) => codigo switch
    {
        _ when codigo.EndsWith(".no_encontrado", StringComparison.Ordinal) => StatusCodes.Status404NotFound,
        _ when codigo.EndsWith(".conflicto", StringComparison.Ordinal) => StatusCodes.Status409Conflict,
        _ when codigo.EndsWith(".bloqueado", StringComparison.Ordinal) => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status400BadRequest,
    };
}
