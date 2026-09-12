using OpenSource1.Core.Common;
using OpenSource1.Core.ValueObjects;

namespace OpenSource1.SmokeTests.Core.ValueObjects;

public class DireccionClienteTests
{
    [Fact]
    public void Constructor_ConLinea1Valida_NormalizaLineas()
    {
        var direccion = new DireccionCliente(" Calle 1 ", " Apto 2 ");

        Assert.Equal("Calle 1", direccion.Linea1);
        Assert.Equal("Apto 2", direccion.Linea2);
    }

    [Fact]
    public void Constructor_ConLinea1Vacia_LanzaErroresDeDominioConCampoLinea1()
    {
        var excepcion = Assert.Throws<ErroresDeDominioException>(() => new DireccionCliente("   ", null));

        var error = Assert.Single(excepcion.Errores);
        Assert.Equal("direccion.linea1_requerida", error.Codigo);
        Assert.Equal("linea1", error.Campo);
    }

    [Fact]
    public void Constructor_ConNombreCampoExplicito_UsaEseNombreEnLugarDelParametro()
    {
        var excepcion = Assert.Throws<ErroresDeDominioException>(
            () => new DireccionCliente("   ", null, "DireccionLinea1"));

        var error = Assert.Single(excepcion.Errores);
        Assert.Equal("DireccionLinea1", error.Campo);
    }
}
