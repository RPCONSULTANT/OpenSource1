extern alias BlazorApp;

using BlazorApp::OpenSource1.Blazor.Components;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Review Focus 2: el returnUrl de las páginas-tarjeta solo acepta rutas locales; todo lo demás vuelve a la lista.</summary>
public sealed class RetornoLocalTests
{
    [Theory]
    [InlineData("/clientes?nombre=ana&pagina=2")]
    [InlineData("/unidades-medida")]
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
    public void Validar_NoLocal_DevuelveLaPredeterminada(string? url) => Assert.Equal("/defecto", RetornoLocal.Validar(url, "/defecto"));

    [Fact]
    public void ConParametro_AgregaOReemplaza()
    {
        Assert.Equal("/clientes?ok=created", RetornoLocal.ConParametro("/clientes", "ok", "created"));
        Assert.Equal("/clientes?pagina=2&ok=created", RetornoLocal.ConParametro("/clientes?ok=deleted&pagina=2", "ok", "created"));
    }

    [Fact]
    public void ConRetorno_CodificaLaRutaDeVuelta()
    {
        Assert.Equal(
            "/unidades-medida/nuevo?returnUrl=%2Funidades-medida%3Fcodigo%3DK%20G",
            RetornoLocal.ConRetorno("/unidades-medida/nuevo", "/unidades-medida?codigo=K G"));
    }
}
