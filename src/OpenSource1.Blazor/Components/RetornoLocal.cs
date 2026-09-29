namespace OpenSource1.Blazor.Components;

/// <summary>
/// returnUrl de las páginas-tarjeta (Fix-Features B1, Review Focus 2): solo rutas locales absolutas ("/…"); se rechazan
/// "//host", "/\host", esquemas ("://", "javascript:") y caracteres de control, y se usa la ruta por defecto.
/// </summary>
public static class RetornoLocal
{
    public static string Validar(string? returnUrl, string predeterminada)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return predeterminada;
        }

        var url = returnUrl;
        var esLocal = url.StartsWith('/')
                      && !url.StartsWith("//", StringComparison.Ordinal)
                      && !url.StartsWith("/\\", StringComparison.Ordinal)
                      && !url.Contains("://", StringComparison.Ordinal)
                      && !url.Any(char.IsControl)
                      && url == url.Trim();
        return esLocal ? url : predeterminada;
    }

    public static string ConRetorno(string destino, string returnUrl) => ConParametro(destino, "returnUrl", returnUrl);

    /// <summary>Añade <paramref name="clave"/>=<paramref name="valor"/> a la query, reemplazando un valor previo de esa clave.</summary>
    public static string ConParametro(string url, string clave, string valor)
    {
        var partes = url.Split('?', 2);
        var pares = partes.Length == 2
            ? partes[1].Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(p => !p.StartsWith(clave + "=", StringComparison.Ordinal) && p != clave)
                .ToList()
            : [];
        pares.Add($"{clave}={Uri.EscapeDataString(valor)}");
        return $"{partes[0]}?{string.Join('&', pares)}";
    }
}
