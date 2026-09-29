using System.Text.RegularExpressions;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>
/// Fix-Features A6: el "flash azul" venía del hero con gradiente animado del inicio (A5 lo retira; lo cubre
/// InicioYGruposTests) y de la barra de progreso de cada navegación. Aquí: ninguna página SSR usa animaciones de entrada,
/// la barra mide 2 px con color atenuado y solo aparece si la navegación tarda más de 150 ms (también al descargar la
/// página: el manejador beforeunload no la muestra de inmediato). ConfirmDialog (modal) queda fuera: su fundido no es una
/// entrada de página.
/// </summary>
public sealed class SinFlashTests
{
    private static readonly Regex AnimacionEntrada = new("animate-(fade-in|slide-up|slide-up-slow|slide-in-left|pop|pulse-glow|float)\\b");

    [Fact]
    public void NingunaPagina_UsaAnimacionesDeEntrada()
    {
        var paginas = Directory.GetFiles(Path.Combine(BlazorSsrFactory.RaizRepositorio(), "src", "OpenSource1.Blazor", "Components", "Pages"), "*.razor")
            .Where(p => !p.EndsWith("Home.razor", StringComparison.Ordinal));

        var conAnimacion = paginas.Where(p => AnimacionEntrada.IsMatch(File.ReadAllText(p))).Select(Path.GetFileName).ToList();

        Assert.True(conAnimacion.Count == 0, "Páginas con animación de entrada: " + string.Join(", ", conAnimacion));
    }

    [Fact]
    public void BarraDeProgreso_Delgada_Atenuada_YTardia()
    {
        var css = File.ReadAllText(Path.Combine(BlazorSsrFactory.RaizRepositorio(), "src", "OpenSource1.Blazor", "wwwroot", "app.css"));
        var js = LeerJs();
        var regla = Regex.Match(css, "\\.blazor-loading-bar\\s*\\{(?<cuerpo>[^}]*)\\}").Groups["cuerpo"].Value;

        Assert.Contains("height: 2px;", regla);
        Assert.DoesNotContain("#2155d9", regla);
        Assert.Contains("rgba(86, 127, 255, 0.55)", regla);
        Assert.Contains("const loadingDelayMs = 150;", js);
        Assert.Contains("overlayPermitido", js);
    }

    [Fact]
    public void BeforeUnload_RespetaElRetardo()
    {
        var js = LeerJs();
        var manejador = CuerpoTrasMarcador(js, "addEventListener('beforeunload'");

        Assert.False(string.IsNullOrWhiteSpace(manejador), "No se encontró el manejador beforeunload.");
        Assert.DoesNotContain("showLoading()", manejador);
        Assert.Contains("scheduleShowLoading()", manejador);
    }

    [Fact]
    public void CuerpoTrasMarcador_BalanceaLlaves()
    {
        // Un showLoading() tras un bloque anidado debe quedar dentro del cuerpo extraído (antes se cortaba en la primera "}").
        const string js = "x.addEventListener('beforeunload', () => {\n  if (a) { scheduleShowLoading(); }\n  showLoading();\n});\nfuera();";

        var cuerpo = CuerpoTrasMarcador(js, "addEventListener('beforeunload'");

        Assert.Contains("showLoading();\n", cuerpo);
        Assert.Contains("scheduleShowLoading()", cuerpo);
        Assert.DoesNotContain("fuera()", cuerpo);
    }

    /// <summary>Cuerpo (sin las llaves exteriores) del primer bloque <c>{…}</c> tras <paramref name="marcador"/>, con llaves balanceadas.</summary>
    private static string CuerpoTrasMarcador(string fuente, string marcador)
    {
        var inicio = fuente.IndexOf(marcador, StringComparison.Ordinal);
        if (inicio < 0)
        {
            return string.Empty;
        }

        var apertura = fuente.IndexOf('{', inicio);
        if (apertura < 0)
        {
            return string.Empty;
        }

        var profundidad = 0;
        for (var i = apertura; i < fuente.Length; i++)
        {
            profundidad += fuente[i] switch { '{' => 1, '}' => -1, _ => 0 };
            if (profundidad == 0)
            {
                return fuente[(apertura + 1)..i];
            }
        }

        return string.Empty;
    }

    private static string LeerJs() =>
        File.ReadAllText(Path.Combine(BlazorSsrFactory.RaizRepositorio(), "src", "OpenSource1.Blazor", "wwwroot", "app.interactions.js"));
}
