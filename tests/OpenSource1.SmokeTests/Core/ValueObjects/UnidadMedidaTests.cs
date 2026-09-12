using OpenSource1.Core.Common;
using OpenSource1.Core.ValueObjects;

namespace OpenSource1.SmokeTests.Core.ValueObjects;

public class UnidadMedidaTests
{
    [Fact]
    public void Of_ConCodigoValido_DevuelveUnidadNormalizada()
    {
        var unidad = UnidadMedida.Of(" und ");

        Assert.Equal("UND", unidad.Codigo);
        Assert.Equal("Unidad", unidad.Nombre);
    }

    [Fact]
    public void Of_ConCodigoVacio_LanzaErroresDeDominioConCampoCodigo()
    {
        var excepcion = Assert.Throws<ErroresDeDominioException>(() => UnidadMedida.Of(""));

        var error = Assert.Single(excepcion.Errores);
        Assert.Equal("unidad_medida.codigo_requerido", error.Codigo);
        Assert.Equal("codigo", error.Campo);
    }

    [Fact]
    public void Of_ConCodigoNoReconocido_LanzaErroresDeDominioConCampoCodigo()
    {
        var excepcion = Assert.Throws<ErroresDeDominioException>(() => UnidadMedida.Of("XYZ"));

        var error = Assert.Single(excepcion.Errores);
        Assert.Equal("unidad_medida.codigo_invalido", error.Codigo);
        Assert.Equal("codigo", error.Campo);
        Assert.Contains("XYZ", error.Mensaje);
    }
}
