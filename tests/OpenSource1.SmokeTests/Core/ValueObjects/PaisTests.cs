using OpenSource1.Core.Common;
using OpenSource1.Core.ValueObjects;

namespace OpenSource1.SmokeTests.Core.ValueObjects;

public class PaisTests
{
    [Fact]
    public void Of_ConCodigoValido_DevuelvePaisNormalizado()
    {
        var pais = Pais.Of(" do ");

        Assert.Equal("DO", pais.Codigo);
        Assert.Equal("República Dominicana", pais.Nombre);
    }

    [Fact]
    public void Of_ConCodigoVacio_LanzaErroresDeDominioConCampoCodigo()
    {
        var excepcion = Assert.Throws<ErroresDeDominioException>(() => Pais.Of("   "));

        var error = Assert.Single(excepcion.Errores);
        Assert.Equal("pais.codigo_requerido", error.Codigo);
        Assert.Equal("codigo", error.Campo);
    }

    [Fact]
    public void Of_ConCodigoNoReconocido_LanzaErroresDeDominioConCampoCodigo()
    {
        var excepcion = Assert.Throws<ErroresDeDominioException>(() => Pais.Of("XX"));

        var error = Assert.Single(excepcion.Errores);
        Assert.Equal("pais.codigo_invalido", error.Codigo);
        Assert.Equal("codigo", error.Campo);
        Assert.Contains("XX", error.Mensaje);
    }

    [Fact]
    public void Of_ConNombreCampoExplicito_UsaEseNombreEnLugarDelParametro()
    {
        var excepcion = Assert.Throws<ErroresDeDominioException>(() => Pais.Of("", "PaisCodigo"));

        var error = Assert.Single(excepcion.Errores);
        Assert.Equal("PaisCodigo", error.Campo);
    }

    [Fact]
    public void Of_ConCodigoInvalidoYNombreCampoExplicito_UsaEseNombreEnLugarDelParametro()
    {
        var excepcion = Assert.Throws<ErroresDeDominioException>(() => Pais.Of("XX", "PaisCodigo"));

        var error = Assert.Single(excepcion.Errores);
        Assert.Equal("PaisCodigo", error.Campo);
    }
}
