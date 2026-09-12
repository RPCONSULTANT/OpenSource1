using OpenSource1.Core.Common;
using OpenSource1.Core.ValueObjects;

namespace OpenSource1.SmokeTests.Core.ValueObjects;

public class CategoriaProductoTests
{
    [Fact]
    public void Constructor_ConDatosValidos_NormalizaCodigoYNombre()
    {
        var categoria = new CategoriaProducto(" gen ", " General ");

        Assert.Equal("GEN", categoria.Codigo);
        Assert.Equal("General", categoria.Nombre);
    }

    [Fact]
    public void Constructor_ConCodigoVacio_LanzaErroresDeDominioConCampoCodigo()
    {
        var excepcion = Assert.Throws<ErroresDeDominioException>(() => new CategoriaProducto("  ", "General"));

        var error = Assert.Single(excepcion.Errores);
        Assert.Equal("categoria_producto.codigo_requerido", error.Codigo);
        Assert.Equal("codigo", error.Campo);
    }

    [Fact]
    public void Constructor_ConNombreVacio_LanzaErroresDeDominioConCampoNombre()
    {
        var excepcion = Assert.Throws<ErroresDeDominioException>(() => new CategoriaProducto("GEN", "  "));

        var error = Assert.Single(excepcion.Errores);
        Assert.Equal("categoria_producto.nombre_requerido", error.Codigo);
        Assert.Equal("nombre", error.Campo);
    }

    [Fact]
    public void Constructor_ConCodigoVacioYNombreCampoExplicito_UsaEseNombreEnLugarDelParametro()
    {
        var excepcion = Assert.Throws<ErroresDeDominioException>(
            () => new CategoriaProducto("  ", "General", "CategoriaCodigo", "CategoriaNombre"));

        var error = Assert.Single(excepcion.Errores);
        Assert.Equal("CategoriaCodigo", error.Campo);
    }

    [Fact]
    public void Constructor_ConNombreVacioYNombreCampoExplicito_UsaEseNombreEnLugarDelParametro()
    {
        var excepcion = Assert.Throws<ErroresDeDominioException>(
            () => new CategoriaProducto("GEN", "  ", "CategoriaCodigo", "CategoriaNombre"));

        var error = Assert.Single(excepcion.Errores);
        Assert.Equal("CategoriaNombre", error.Campo);
    }
}
