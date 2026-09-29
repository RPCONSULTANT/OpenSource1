using System.Net;
using System.Text.RegularExpressions;

namespace OpenSource1.SmokeTests.TestInfrastructure;

/// <summary>
/// Envía un EditForm SSR como lo haría el navegador: GET de la página (fija la cookie de antiforgery en el cliente),
/// extrae el token oculto y hace POST con <c>_handler</c> = FormName. Devuelve la respuesta sin seguir redirecciones.
/// </summary>
public static partial class FormulariosSsr
{
    public static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient cliente, string urlGet, string formName, IReadOnlyDictionary<string, string> campos, string? urlPost = null)
    {
        var pagina = await cliente.GetAsync(urlGet);
        var html = await pagina.Content.ReadAsStringAsync();
        Assert.True(pagina.StatusCode == HttpStatusCode.OK, $"GET {urlGet} devolvió {pagina.StatusCode}: {html}");

        var etiqueta = TokenInput().Match(html);
        Assert.True(etiqueta.Success, $"La página {urlGet} no contiene el token antiforgery.");
        var valor = ValorAtributo().Match(etiqueta.Value);
        Assert.True(valor.Success, "El input del token no tiene value.");

        var cuerpo = new Dictionary<string, string>(campos)
        {
            ["_handler"] = formName,
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(valor.Groups[1].Value),
        };

        return await cliente.PostAsync(urlPost ?? urlGet, new FormUrlEncodedContent(cuerpo));
    }

    /// <summary>Ruta y query del <c>Location</c> de una redirección (absoluto o relativo).</summary>
    public static string Destino(HttpResponseMessage respuesta)
    {
        var location = respuesta.Headers.Location ?? throw new InvalidOperationException($"La respuesta {respuesta.StatusCode} no trae Location.");
        return location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>")]
    private static partial Regex TokenInput();

    [GeneratedRegex("value=\"([^\"]+)\"")]
    private static partial Regex ValorAtributo();
}
