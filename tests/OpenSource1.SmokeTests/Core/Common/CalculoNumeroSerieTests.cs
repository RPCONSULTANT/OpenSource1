using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.SmokeTests.Core.Common;

public sealed class CalculoNumeroSerieTests
{
    /// <summary>Final review 4: la página posteada solo muestra un ?aviso= con la forma exacta que genera el posteo.</summary>
    [Fact]
    public void EsTextoAviso_ReconoceElAvisoGenerado_YRechazaTextoLibre()
    {
        var generado = Ok(CalculoNumeroSerie.Siguiente("FV", "FV-000001", "FV-000100", "FV-000090", 1, "FV-000091")).Aviso;
        var soloDigitos = Ok(CalculoNumeroSerie.Siguiente("FV", "00000001", "00000100", "00000090", 1, "00000091")).Aviso;

        Assert.True(CalculoNumeroSerie.EsTextoAviso(generado));
        Assert.True(CalculoNumeroSerie.EsTextoAviso(soloDigitos));
        Assert.False(CalculoNumeroSerie.EsTextoAviso(null));
        Assert.False(CalculoNumeroSerie.EsTextoAviso(""));
        Assert.False(CalculoNumeroSerie.EsTextoAviso("Quedan 3"));
        Assert.False(CalculoNumeroSerie.EsTextoAviso("Su cuenta fue bloqueada: llame al 555-0100."));
        Assert.False(CalculoNumeroSerie.EsTextoAviso(generado + " Llame al 555-0100."));
        Assert.False(CalculoNumeroSerie.EsTextoAviso(
            "La serie FV alcanzó su número de aviso (llame al 555 0100): quedan 3 número(s) en la línea."));
        Assert.False(CalculoNumeroSerie.EsTextoAviso(
            "La serie PAGUE EN EFECTIVO alcanzó su número de aviso (FV-000091): quedan 3 número(s) en la línea."));
    }

    [Theory]
    [InlineData("00000001", "00000000", false)]
    [InlineData("00000001", "", false)]
    [InlineData("00000001", null, false)]
    [InlineData("00000001", "00000001", true)]
    [InlineData("00000001", "7", true)]
    [InlineData("FV-000010", "FV-000009", false)]
    [InlineData("FV-000010", "FV-7", false)]
    [InlineData("00000001", "abc", true)]
    [InlineData("00000001", " 7", true)]
    [InlineData("FV-000010", "NC-000001", true)]
    [InlineData("FV-000010", "FV-0000001", true)]
    [InlineData("00000001", "1234567890123456789", true)]
    public void EstaUsada_SoloSiElUltimoNoEsMenorQueElInicial(string inicial, string? ultimo, bool usada)
    {
        Assert.Equal(usada, CalculoNumeroSerie.EstaUsada(inicial, ultimo));
    }

    [Fact]
    public void SoloDigitos_ComportamientoIdenticoAlAnterior()
    {
        Assert.Equal("00000001", Ok(CalculoNumeroSerie.Siguiente("FV", "00000001", "99999999", "00000000", 1, null)).Numero);
        Assert.Equal("00000042", Ok(CalculoNumeroSerie.Siguiente("FV", "00000001", "99999999", "00000041", 1, null)).Numero);
        // Contador heredado sin relleno (Review Focus 4).
        Assert.Equal("00000008", Ok(CalculoNumeroSerie.Siguiente("FV", "00000001", "99999999", "7", 1, null)).Numero);
    }

    [Fact]
    public void ConPrefijo_IncrementaLaParteNumericaYConservaPrefijoYAncho()
    {
        Assert.Equal("FV-000010", Ok(CalculoNumeroSerie.Siguiente("FV", "FV-000001", "FV-999999", "FV-000009", 1, null)).Numero);
    }

    [Fact]
    public void LineaNoUsada_EmpiezaEnElNumeroInicial_AunqueElIncrementoSeaMayorQueUno()
    {
        Assert.Equal("A-0005", Ok(CalculoNumeroSerie.Siguiente("A", "A-0005", "A-9999", "", 5, null)).Numero);
        Assert.Equal("A-0010", Ok(CalculoNumeroSerie.Siguiente("A", "A-0005", "A-9999", "A-0005", 5, null)).Numero);
    }

    [Fact]
    public void Agotada_CuandoElSiguienteSuperaElFinal()
    {
        var resultado = CalculoNumeroSerie.Siguiente("X", "00001", "00001", "00001", 1, null);

        Assert.Equal(("numeracion.serie_agotada", "SerieId"), (resultado.Errores[0].Codigo, resultado.Errores[0].Campo));
    }

    [Theory]
    [InlineData("FV-000001", "NC-999999")]
    [InlineData("FV-000001", "FV-9999999")]
    [InlineData("sin-digitos", "FV-999999")]
    public void LineaConFormatoInvalido_Falla(string inicial, string final)
    {
        var resultado = CalculoNumeroSerie.Siguiente("X", inicial, final, "", 1, null);

        Assert.Equal("numeracion.linea_invalida", resultado.Errores[0].Codigo);
    }

    [Fact]
    public void NumeroDeAviso_AvisaAlAlcanzarlo_YNoAntes()
    {
        var antes = Ok(CalculoNumeroSerie.Siguiente("FV", "FV-000001", "FV-000100", "FV-000089", 1, "FV-000091"));
        var justo = Ok(CalculoNumeroSerie.Siguiente("FV", "FV-000001", "FV-000100", "FV-000090", 1, "FV-000091"));

        Assert.Null(antes.Aviso);
        Assert.NotNull(justo.Aviso);
        Assert.Contains("FV-000091", justo.Aviso);
        Assert.Contains("9 número(s)", justo.Aviso);
    }

    [Fact]
    public void SiguienteIgualAlFinal_SeEmite()
    {
        Assert.Equal("A-9999", Ok(CalculoNumeroSerie.Siguiente("A", "A-0001", "A-9999", "A-9998", 1, null)).Numero);
    }

    [Fact]
    public void FinalDeOchoNueves_SeAgota_SinEmitirUnNumeroMasAncho()
    {
        var resultado = CalculoNumeroSerie.Siguiente("X", "00000001", "99999999", "99999999", 1, null);

        Assert.Equal("numeracion.serie_agotada", resultado.Errores[0].Codigo);
    }

    [Fact]
    public void IncrementoMayorQueUnoQueSaltaElFinal_SeAgota()
    {
        var resultado = CalculoNumeroSerie.Siguiente("A", "A-0001", "A-9999", "A-9995", 5, null);

        Assert.Equal("numeracion.serie_agotada", resultado.Errores[0].Codigo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void IncrementoCeroONegativo_LineaInvalida(int incremento)
    {
        var resultado = CalculoNumeroSerie.Siguiente("A", "A-0001", "A-9999", "A-0005", incremento, null);

        Assert.Equal(("numeracion.linea_invalida", "SerieId"), (resultado.Errores[0].Codigo, resultado.Errores[0].Campo));
    }

    [Fact]
    public void RangoInvertido_LineaInvalida()
    {
        var resultado = CalculoNumeroSerie.Siguiente("A", "A-0010", "A-0001", "", 1, null);

        Assert.Equal("numeracion.linea_invalida", resultado.Errores[0].Codigo);
    }

    [Theory]
    [InlineData("FV-000001", "abc")]
    [InlineData("FV-000001", " FV-000007")]
    [InlineData("FV-000001", "FV-")]
    [InlineData("FV-000001", "NC-000009")]
    [InlineData("FV-000001", "fv-000009")]
    [InlineData("FV-000001", "FV-0000009")]
    [InlineData("00000001", "000000007")]
    [InlineData("00000001", "1234567890123456789")]
    public void UltimoUsadoNoInterpretableOConOtroFormato_LineaInvalida(string inicial, string ultimo)
    {
        var final = inicial.StartsWith("FV-", StringComparison.Ordinal) ? "FV-999999" : "99999999";

        var resultado = CalculoNumeroSerie.Siguiente("X", inicial, final, ultimo, 1, null);

        Assert.Equal("numeracion.linea_invalida", resultado.Errores[0].Codigo);
    }

    [Fact]
    public void UltimoUsadoHeredadoSinRelleno_MismoPrefijoYAnchoMenor_SeAcepta()
    {
        Assert.Equal("FV-000008", Ok(CalculoNumeroSerie.Siguiente("FV", "FV-000001", "FV-999999", "FV-7", 1, null)).Numero);
    }

    [Theory]
    [InlineData("")]
    [InlineData("basura")]
    [InlineData("FV-")]
    public void NumeroDeAvisoVacioOInvalido_SeIgnora(string aviso)
    {
        Assert.Null(Ok(CalculoNumeroSerie.Siguiente("FV", "FV-000001", "FV-000100", "FV-000099", 1, aviso)).Aviso);
    }

    [Fact]
    public void Nombres_CubrenLosOchoTipos()
    {
        Assert.Equal(8, TipoDocumentoSerieNombres.Todos.Count);
        Assert.All(TipoDocumentoSerieNombres.Todos, t => Assert.NotEqual(t.ToString(), TipoDocumentoSerieNombres.Nombre(t)));
        Assert.False(TipoDocumentoSerieNombres.EsValido((TipoDocumentoSerie)0));
    }

    private static NumeroGenerado Ok(Result<NumeroGenerado> resultado)
    {
        Assert.True(resultado.EsExito, resultado.EsFallo ? resultado.Errores[0].Mensaje : string.Empty);
        return resultado.Valor;
    }
}
