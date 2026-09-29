using System.Security.Claims;

namespace OpenSource1.Blazor.Navigation;

public interface IRegistroModulos
{
    IReadOnlyList<GrupoModulo> Grupos { get; }

    IReadOnlyList<Modulo> Todos { get; }

    /// <summary>Módulos que el usuario puede abrir (política Y rol), en el orden del catálogo. Anónimo: ninguno.</summary>
    Task<IReadOnlyList<Modulo>> VisiblesAsync(ClaimsPrincipal usuario);

    /// <summary>Grupos con al menos un módulo visible, por <c>Orden</c>.</summary>
    Task<IReadOnlyList<GrupoModulo>> GruposVisiblesAsync(ClaimsPrincipal usuario);

    GrupoModulo? BuscarGrupo(string? clave);

    /// <summary>Módulo cuya ruta es el prefijo (por segmentos) más largo de <paramref name="ruta"/>; query ignorada.</summary>
    Modulo? ModuloDeRuta(string? ruta);
}
