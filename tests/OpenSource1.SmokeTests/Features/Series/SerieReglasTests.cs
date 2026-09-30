using OpenSource1.Application.Features.Series;
using OpenSource1.Core.Enums;

namespace OpenSource1.SmokeTests.Features.Series;

public sealed class SerieReglasTests
{
    private static readonly DateOnly Hoy = new(2026, 9, 29);

    [Theory]
    [InlineData("FV-000001", "NC-999999", "", "linea_serie.formato_distinto", "NumeroFinal")]
    [InlineData("FV-000001", "FV-9999999", "", "linea_serie.formato_distinto", "NumeroFinal")]
    [InlineData("FV-000009", "FV-000001", "", "linea_serie.rango_invalido", "NumeroFinal")]
    [InlineData("FV-", "FV-000001", "", "linea_serie.numero_invalido", "NumeroInicial")]
    [InlineData("FV-000001", "FV-999999", "FV-9999999", "linea_serie.aviso_invalido", "NumeroAviso")]
    public void ValidarLinea_RechazaFormatoYRango(string inicial, string final, string aviso, string codigo, string campo)
    {
        var errores = SerieReglas.ValidarLinea(inicial, final, aviso, null, 1, Hoy);

        Assert.Contains(errores, e => e.Codigo == codigo && e.Campo == campo);
    }

    [Theory]
    [InlineData("FV-000000", "FV-000009")]
    [InlineData("0", "9")]
    public void ValidarLinea_NumeroInicialDebeSerUnoOMayor(string inicial, string final)
    {
        var errores = SerieReglas.ValidarLinea(inicial, final, null, null, 1, Hoy);

        Assert.Contains(errores, e => e.Codigo == "linea_serie.rango_invalido" && e.Campo == "NumeroInicial");
    }

    [Theory]
    [InlineData("FV-000001", "FV-000001", "FV-000009")]
    [InlineData("FV-000009", "FV-000001", "FV-000009")]
    public void ValidarLinea_AvisoEnLosExtremosDelRango_Valido(string aviso, string inicial, string final)
    {
        Assert.Empty(SerieReglas.ValidarLinea(inicial, final, aviso, null, 1, Hoy));
    }

    [Theory]
    [InlineData("FV-000010")]
    [InlineData("NC-000005")]
    [InlineData("FV-00005")]
    public void ValidarLinea_AvisoFueraDelRangoOConOtroFormato(string aviso)
    {
        var errores = SerieReglas.ValidarLinea("FV-000001", "FV-000009", aviso, null, 1, Hoy);

        Assert.Contains(errores, e => e.Codigo == "linea_serie.aviso_invalido" && e.Campo == "NumeroAviso");
    }

    [Theory]
    [InlineData("FV-000000")]
    [InlineData("FV-1000000")]
    [InlineData("NC-000005")]
    public void ValidarLinea_UltimoUsadoFueraDelRangoOConOtroFormato(string ultimo)
    {
        var errores = SerieReglas.ValidarLinea("FV-000001", "FV-999999", null, ultimo, 1, Hoy);

        Assert.Contains(errores, e => e.Codigo == "linea_serie.ultimo_invalido");
    }

    [Fact]
    public void ValidarLinea_IncrementoYFecha()
    {
        var errores = SerieReglas.ValidarLinea("00001", "00009", null, null, 0, default);

        Assert.Contains(errores, e => e.Campo == "Incremento");
        Assert.Contains(errores, e => e.Campo == "FechaInicial");
    }

    [Fact]
    public void Solapada_MismoPrefijoYAnchoConRangoCruzado_OtroFormatoNo()
    {
        var existentes = new[] { new LineaDeTipo(Guid.NewGuid(), Guid.NewGuid(), "FV", "00000001", "99999999") };

        Assert.Equal("linea_serie.solapada", SerieReglas.Solapada("00000500", "00000600", existentes, null)!.Value.Codigo);
        Assert.Null(SerieReglas.Solapada("X-00000001", "X-99999999", existentes, null));
        Assert.Null(SerieReglas.Solapada("0000001", "9999999", existentes, null));
        Assert.Null(SerieReglas.Solapada("00000500", "00000600", existentes, existentes[0].LineaId));
    }

    [Theory]
    [InlineData("fv-01", "FV-01")]
    [InlineData("  ab_c ", "AB_C")]
    public void NormalizarCodigo(string entrada, string esperado) => Assert.Equal(esperado, SerieReglas.NormalizarCodigo(entrada));

    [Fact]
    public void ValidarCabecera_TipoYCodigo()
    {
        var errores = SerieReglas.ValidarCabecera("MAL CODIGO", "", (TipoDocumentoSerie)0);

        Assert.Contains(errores, e => e.Campo == "Codigo");
        Assert.Contains(errores, e => e.Campo == "Descripcion");
        Assert.Contains(errores, e => e.Campo == "TipoDocumento");
    }
}
