using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace OpenSource1.Blazor.Navigation;

public sealed class RegistroModulos(IAuthorizationService autorizacion) : IRegistroModulos
{
    public IReadOnlyList<GrupoModulo> Grupos => CatalogoModulos.Grupos;

    public IReadOnlyList<Modulo> Todos => CatalogoModulos.Modulos;

    public async Task<IReadOnlyList<Modulo>> VisiblesAsync(ClaimsPrincipal usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        if (usuario.Identity?.IsAuthenticated != true)
        {
            return [];
        }

        var visibles = new List<Modulo>();
        foreach (var modulo in Todos)
        {
            if (modulo.Roles is { Count: > 0 } roles && !roles.Any(usuario.IsInRole))
            {
                continue;
            }

            if (modulo.Politica is { } politica && !(await autorizacion.AuthorizeAsync(usuario, politica)).Succeeded)
            {
                continue;
            }

            visibles.Add(modulo);
        }

        return visibles;
    }

    public async Task<IReadOnlyList<GrupoModulo>> GruposVisiblesAsync(ClaimsPrincipal usuario)
    {
        var claves = (await VisiblesAsync(usuario)).Select(m => m.Grupo).ToHashSet(StringComparer.Ordinal);
        return Grupos.Where(g => claves.Contains(g.Clave)).OrderBy(g => g.Orden).ToList();
    }

    public GrupoModulo? BuscarGrupo(string? clave) =>
        Grupos.FirstOrDefault(g => string.Equals(g.Clave, clave, StringComparison.OrdinalIgnoreCase));

    public Modulo? ModuloDeRuta(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta))
        {
            return null;
        }

        var sinQuery = ruta.Split('?', '#')[0];
        var camino = "/" + sinQuery.Trim('/');
        if (camino == "/")
        {
            return null;
        }

        // Candidatos: la ruta de cada módulo y las rutas asociadas (altas de documentos); gana el prefijo más largo.
        var candidatos = Todos.Select(m => (m.Ruta, Modulo: (Modulo?)m))
            .Concat(CatalogoModulos.RutasAsociadas.Select(a => (Ruta: a.Key, Modulo: Todos.FirstOrDefault(m => m.Clave == a.Value))));
        return candidatos
            .Where(c => c.Modulo is not null && EsPrefijo(c.Ruta, camino))
            .OrderByDescending(c => c.Ruta.Length)
            .Select(c => c.Modulo)
            .FirstOrDefault();
    }

    private static bool EsPrefijo(string ruta, string camino) =>
        string.Equals(camino, ruta, StringComparison.OrdinalIgnoreCase)
        || camino.StartsWith(ruta + "/", StringComparison.OrdinalIgnoreCase);
}
