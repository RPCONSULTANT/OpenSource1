namespace OpenSource1.Blazor.Navigation;

/// <summary>Grupo del menú, del inicio y de <c>/modulos/{Clave}</c>. <c>Icono</c> es el trazo <c>d</c> de un SVG 24×24.</summary>
public sealed record GrupoModulo(string Clave, string Titulo, string Icono, string Descripcion, int Orden);

/// <summary>
/// Módulo navegable. Visible si el usuario cumple <c>Politica</c> (si la hay) Y tiene alguno de <c>Roles</c> (si los hay).
/// <c>Ruta</c> es la ruta absoluta del listado sin query; <c>PalabrasClave</c> alimenta la búsqueda de módulos.
/// </summary>
public sealed record Modulo(
    string Clave,
    string Grupo,
    string Titulo,
    string Descripcion,
    string Ruta,
    string Icono,
    string? Politica,
    IReadOnlyList<string>? Roles,
    IReadOnlyList<string> PalabrasClave);
