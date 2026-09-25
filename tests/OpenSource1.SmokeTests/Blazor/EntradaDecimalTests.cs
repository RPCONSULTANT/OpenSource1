extern alias BlazorApp;
using BlazorApp::OpenSource1.Blazor.Components;

namespace OpenSource1.SmokeTests.Blazor;

public sealed class EntradaDecimalTests
{
    [Theory]
    [InlineData("1500.50", "1500.50")]
    [InlineData("1500,50", "1500.50")]
    [InlineData("1500", "1500")]
    [InlineData("0", "0")]
    [InlineData("", "0")]
    [InlineData("   ", "0")]
    [InlineData(" 1 500,5 ", "1500.5")]
    [InlineData("1.500,50", "1500.50")]
    [InlineData("1,500.50", "1500.50")]
    [InlineData("1,500,000", "1500000")]
    [InlineData("1.500.000", "1500000")]
    [InlineData("1,500,000.25", "1500000.25")]
    [InlineData("1500.5", "1500.5")]
    [InlineData("1500,5", "1500.5")]
    [InlineData("1500.5000", "1500.5000")]
    [InlineData("1500.500", "1500.500")]
    [InlineData("0,500", "0.500")]
    [InlineData("0.125", "0.125")]
    [InlineData("99999999999999.9999", "99999999999999.9999")]
    [InlineData("1 500 000,25", "1500000.25")]
    [InlineData("1\u2009500,5", "1500.5")]
    [InlineData("1\u00A0500", "1500")]
    [InlineData("-1 500", "-1500")]
    public void TryParse_AceptaPuntoOComaDecimal(string entrada, string esperado)
    {
        Assert.True(EntradaDecimal.TryParse(entrada, out var valor, out var error), error);
        Assert.Equal(decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture), valor);
    }

    [Theory]
    [InlineData("1,500")]
    [InlineData("1.500")]
    [InlineData("12,345")]
    [InlineData("999.999")]
    public void TryParse_UnSoloSeparadorConTresDigitos_EsAmbiguoYSeRechaza(string entrada)
    {
        Assert.False(EntradaDecimal.TryParse(entrada, out _, out var error));
        Assert.Equal(EntradaDecimal.MensajeAmbiguo, error);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1a")]
    [InlineData("1,,5")]
    [InlineData("1..5")]
    [InlineData("1,5,5")]
    [InlineData("1.5.5")]
    [InlineData("1.234,56,7")]
    [InlineData(",5")]
    [InlineData("5,")]
    [InlineData("1e5")]
    [InlineData("$100")]
    [InlineData("12,34.5,6")]
    [InlineData("1 5 0 0")]
    [InlineData("1 50")]
    [InlineData("12 3456")]
    [InlineData("1  500")]
    [InlineData("1 500 5")]
    [InlineData("- 5")]
    [InlineData("1 ,5")]
    [InlineData("99999999999999999999999999999999")]
    public void TryParse_TextoNoNumerico_SeRechaza(string entrada)
    {
        Assert.False(EntradaDecimal.TryParse(entrada, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_Negativo_SeLeeConSignoParaQueLaApiLoRechace()
    {
        Assert.True(EntradaDecimal.TryParse("-5,25", out var valor, out _));
        Assert.Equal(-5.25m, valor);
    }

    [Theory]
    [InlineData(0, "0.00")]
    [InlineData(1500.5, "1500.50")]
    [InlineData(1500.5075, "1500.5075")]
    [InlineData(25000, "25000.00")]
    public void Formatear_UsaPuntoDecimalSinMiles(double valor, string esperado)
    {
        Assert.Equal(esperado, EntradaDecimal.Formatear((decimal)valor));
    }
}
