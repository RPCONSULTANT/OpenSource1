using System.Net;

namespace OpenSource1.SmokeTests.TestInfrastructure;

/// <summary>
/// HTML de páginas SSR listo para afirmar texto visible: Razor codifica los acentos (<c>&amp;#xE1;</c>…), así que las
/// aserciones se hacen siempre sobre el HTML decodificado. Helper único: no copiar <c>HtmlAsync</c> en cada test.
/// </summary>
public static class HtmlSsr
{
    /// <summary>GET de <paramref name="url"/>; exige 200 y devuelve el cuerpo decodificado.</summary>
    public static async Task<string> HtmlAsync(HttpClient cliente, string url)
    {
        var respuesta = await cliente.GetAsync(url);
        var html = await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"GET {url} devolvió {respuesta.StatusCode}: {html}");
        return Decodificar(html);
    }

    /// <summary>Decodifica entidades HTML (<see cref="WebUtility.HtmlDecode(string?)"/>).</summary>
    public static string Decodificar(string html) => WebUtility.HtmlDecode(html);
}
