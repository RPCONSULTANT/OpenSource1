using OpenSource1.Core.Common;
using OpenSource1.Core.ValueObjects;

namespace OpenSource1.SmokeTests.Core.ValueObjects;

public class SectorTests
{
    [Fact]
    public void Constructor_ConNombreValido_NormalizaNombre()
    {
        var sector = new Sector(" Zona Norte ");

        Assert.Equal("Zona Norte", sector.Nombre);
    }

    [Fact]
    public void Constructor_ConNombreVacio_LanzaErroresDeDominioConCampoNombre()
    {
        var excepcion = Assert.Throws<ErroresDeDominioException>(() => new Sector("   "));

        var error = Assert.Single(excepcion.Errores);
        Assert.Equal("sector.nombre_requerido", error.Codigo);
        Assert.Equal("nombre", error.Campo);
    }

    [Fact]
    public void Constructor_ConNombreCampoExplicito_UsaEseNombreEnLugarDelParametro()
    {
        var excepcion = Assert.Throws<ErroresDeDominioException>(() => new Sector("   ", "Sector"));

        var error = Assert.Single(excepcion.Errores);
        Assert.Equal("Sector", error.Campo);
    }
}
