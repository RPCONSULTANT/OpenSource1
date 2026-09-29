using System.Security.Claims;

namespace OpenSource1.Blazor.Navigation;

public sealed record Indicador(string Titulo, string Valor, string? Ruta);

public interface IIndicadoresModulos
{
    /// <summary>Hasta 3 indicadores del grupo desde endpoints existentes; el que falla se omite. Nunca lanza. Sin CanConsult: ninguno.</summary>
    Task<IReadOnlyList<Indicador>> ObtenerAsync(string grupo, ClaimsPrincipal usuario, CancellationToken cancellationToken = default);
}
