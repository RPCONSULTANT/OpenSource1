extern alias BlazorApp;

using BlazorApp::OpenSource1.Blazor.Components;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Review Focus 2: el returnUrl de las páginas-tarjeta solo acepta rutas locales; todo lo demás vuelve a la lista.</summary>
public sealed class RetornoLocalTests
{
    [Theory]
    [InlineData("/clientes?nombre=ana&pagina=2")]
    [InlineData("/unidades-medida")]
    [InlineData("/%2F/evil.com")]
    public void Validar_RutaLocal_SeConserva(string url) => Assert.Equal(url, RetornoLocal.Validar(url, "/defecto"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://evil.com")]
    [InlineData("//evil.com")]
    [InlineData("/\\evil.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("clientes")]
    [InlineData("/clientes\n")]
    [InlineData("/redir?to=http://evil.com")]
    [InlineData("/\t/evil.com")]
    [InlineData("\\\\evil.com")]
    public void Validar_NoLocal_DevuelveLaPredeterminada(string? url) => Assert.Equal("/defecto", RetornoLocal.Validar(url, "/defecto"));

    [Fact]
    public void ConParametro_AgregaOReemplaza()
    {
        Assert.Equal("/clientes?ok=created", RetornoLocal.ConParametro("/clientes", "ok", "created"));
        Assert.Equal("/clientes?pagina=2&ok=created", RetornoLocal.ConParametro("/clientes?ok=deleted&pagina=2", "ok", "created"));
    }

    [Fact]
    public void ConParametro_VaAntesDelFragmento()
    {
        Assert.Equal("/x?ok=1#a", RetornoLocal.ConParametro("/x#a", "ok", "1"));
        Assert.Equal("/x?p=2&ok=1#a", RetornoLocal.ConParametro("/x?p=2&ok=0#a", "ok", "1"));
    }

    [Theory]
    [InlineData("/clientes?nombre=ana&ok=created&pagina=2&deleteId=d1&editId=e1&sel=s1", "/clientes?nombre=ana&pagina=2&sel=s1")]
    [InlineData("/clientes?ok=deleted", "/clientes")]
    [InlineData("/clientes?editId=e1#tabla", "/clientes#tabla")]
    [InlineData("/clientes?okey=1&xeditId=2", "/clientes?okey=1&xeditId=2")]
    [InlineData("/clientes", "/clientes")]
    // Ola final (Minor 7): [SupplyParameterFromQuery] ignora mayúsculas; SinTransitorios también.
    [InlineData("/clientes?OK=created&DeleteId=d1&EDITID=e1&Sel=s1", "/clientes?Sel=s1")]
    [InlineData("/clientes?Ok&OKEY=1", "/clientes?OKEY=1")]
    public void SinTransitorios_QuitaOkDeleteIdYEditId(string url, string esperado) =>
        Assert.Equal(esperado, RetornoLocal.SinTransitorios(url));

    [Theory]
    [InlineData("/clientes?pagina=2&editId=e1", "/clientes?pagina=2")]
    [InlineData("//evil.com", "/clientes")]
    [InlineData(null, "/clientes")]
    public void Volver_ValidaYQuitaTransitorios(string? returnUrl, string esperado) =>
        Assert.Equal(esperado, RetornoLocal.Volver(returnUrl, "/clientes"));

    [Fact]
    public void ConRetorno_CodificaLaRutaDeVuelta()
    {
        Assert.Equal(
            "/unidades-medida/nuevo?returnUrl=%2Funidades-medida%3Fcodigo%3DK%20G",
            RetornoLocal.ConRetorno("/unidades-medida/nuevo", "/unidades-medida?codigo=K G"));
    }
}
