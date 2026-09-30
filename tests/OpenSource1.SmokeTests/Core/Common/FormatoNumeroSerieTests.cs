using OpenSource1.Core.Common;

namespace OpenSource1.SmokeTests.Core.Common;

public sealed class FormatoNumeroSerieTests
{
    [Theory]
    [InlineData("FV-000001", "FV-", 1L, 6)]
    [InlineData("00000001", "", 1L, 8)]
    [InlineData("A1B-0099", "A1B-", 99L, 4)]
    [InlineData("NC 2026/0005", "NC 2026/", 5L, 4)]
    [InlineData("7", "", 7L, 1)]
    public void TryParse_SeparaPrefijoValorYAncho(string texto, string prefijo, long valor, int ancho)
    {
        Assert.True(FormatoNumeroSerie.TryParse(texto, out var numero));
        Assert.Equal(new NumeroSerie(prefijo, valor, ancho), numero);
        Assert.Equal(texto, numero.Texto);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("FV-")]
    [InlineData(" FV-0001")]
    [InlineData("FV-0001 ")]
    [InlineData("FV-0001234567890123456789")]
    [InlineData("1234567890123456789")]
    public void TryParse_Rechaza_SinDigitosFinales_EspaciosExteriores_MasDe20CaracteresOMasDe18Digitos(string? texto)
    {
        Assert.False(FormatoNumeroSerie.TryParse(texto, out _));
    }

    [Fact]
    public void Formatear_RellenaConCerosHastaElAncho()
    {
        Assert.Equal("FV-000042", FormatoNumeroSerie.Formatear("FV-", 42, 6));
        Assert.Equal("00000001", FormatoNumeroSerie.Formatear("", 1, 8));
    }

    [Fact]
    public void MismoFormato_ExigeMismoPrefijoYMismoAncho()
    {
        FormatoNumeroSerie.TryParse("FV-000001", out var a);
        FormatoNumeroSerie.TryParse("FV-999999", out var b);
        FormatoNumeroSerie.TryParse("FV-0000001", out var c);
        FormatoNumeroSerie.TryParse("fv-000001", out var d);

        Assert.True(a.MismoFormato(b));
        Assert.False(a.MismoFormato(c));
        Assert.False(a.MismoFormato(d));
    }
}
