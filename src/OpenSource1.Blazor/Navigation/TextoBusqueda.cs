using System.Globalization;
using System.Text;

namespace OpenSource1.Blazor.Navigation;

/// <summary>Búsqueda de módulos sin acentos ni mayúsculas (misma regla que <c>app.search.js</c>).</summary>
public static class TextoBusqueda
{
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var descompuesto = texto.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    /// <summary>Módulos cuyo título, grupo o palabras clave contienen la consulta. Consulta vacía: ninguno.</summary>
    public static IReadOnlyList<Modulo> FiltrarModulos(IEnumerable<Modulo> modulos, string? consulta)
    {
        var q = Normalizar(consulta);
        if (q.Length == 0)
        {
            return [];
        }

        var titulosGrupo = CatalogoModulos.Grupos.ToDictionary(g => g.Clave, g => g.Titulo, StringComparer.Ordinal);
        return modulos
            .Where(m => Normalizar($"{m.Titulo} {titulosGrupo.GetValueOrDefault(m.Grupo)} {string.Join(' ', m.PalabrasClave)}").Contains(q, StringComparison.Ordinal))
            .ToList();
    }
}
