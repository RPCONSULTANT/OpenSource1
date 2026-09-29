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

    /// <summary>
    /// Ruta de vuelta de un editor: <see cref="Validar"/> y después <see cref="SinTransitorios"/> (nunca vuelve con ok,
    /// deleteId ni editId, que reabrirían el aviso, el diálogo de borrado o el editor).
    /// </summary>
    public static string Volver(string? returnUrl, string predeterminada) => SinTransitorios(Validar(returnUrl, predeterminada));

    /// <summary>destino?returnUrl=… con la ruta de vuelta limpia de parámetros transitorios.</summary>
    public static string ConRetorno(string destino, string returnUrl) => ConParametro(destino, "returnUrl", SinTransitorios(returnUrl));

    /// <summary>Parámetros de un solo uso que nunca forman parte de una ruta de vuelta.</summary>
    public static readonly IReadOnlyList<string> Transitorios = ["ok", "deleteId", "editId"];

    /// <summary>Quita de la query de <paramref name="url"/> los parámetros <see cref="Transitorios"/> (conserva filtros, página, sel y #fragmento).</summary>
    public static string SinTransitorios(string url)
    {
        var (ruta, pares, fragmento) = Partir(url);
        pares.RemoveAll(p => Transitorios.Any(clave => EsClave(p, clave)));
        return Unir(ruta, pares, fragmento);
    }

    /// <summary>Añade <paramref name="clave"/>=<paramref name="valor"/> a la query (antes del #fragmento), reemplazando un valor previo de esa clave.</summary>
    public static string ConParametro(string url, string clave, string valor)
    {
        var (ruta, pares, fragmento) = Partir(url);
        pares.RemoveAll(p => EsClave(p, clave));
        pares.Add($"{clave}={Uri.EscapeDataString(valor)}");
        return Unir(ruta, pares, fragmento);
    }

    // Sin distinguir mayúsculas, igual que el binder de [SupplyParameterFromQuery]: ?OK= o ?DeleteId= también son transitorios.
    private static bool EsClave(string par, string clave) =>
        par.Equals(clave, StringComparison.OrdinalIgnoreCase) || par.StartsWith(clave + "=", StringComparison.OrdinalIgnoreCase);

    private static (string Ruta, List<string> Pares, string Fragmento) Partir(string url)
    {
        var almohadilla = url.IndexOf('#');
        var fragmento = almohadilla < 0 ? string.Empty : url[almohadilla..];
        var sinFragmento = almohadilla < 0 ? url : url[..almohadilla];
        var partes = sinFragmento.Split('?', 2);
        var pares = partes.Length == 2 ? partes[1].Split('&', StringSplitOptions.RemoveEmptyEntries).ToList() : [];
        return (partes[0], pares, fragmento);
    }

    private static string Unir(string ruta, List<string> pares, string fragmento) =>
        pares.Count == 0 ? ruta + fragmento : $"{ruta}?{string.Join('&', pares)}{fragmento}";
}
