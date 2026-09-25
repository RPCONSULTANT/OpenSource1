using OpenSource1.Core.Common;

namespace OpenSource1.SmokeTests.Core.Common;

public class ResultTests
{
    [Fact]
    public void Exito_NoTieneErrores()
    {
        var resultado = Result.Exito();

        Assert.True(resultado.EsExito);
        Assert.Empty(resultado.Errores);
    }

    [Fact]
    public void Fallo_ConservaLosErrores()
    {
        var resultado = Result.Fallo(new Error("producto.no_encontrado", "No existe.", "Id"));

        Assert.False(resultado.EsExito);
        var error = Assert.Single(resultado.Errores);
        Assert.Equal("producto.no_encontrado", error.Codigo);
        Assert.Equal("Id", error.Campo);
    }

    [Fact]
    public void ResultGenerico_ExitoExponeElValor()
    {
        var resultado = Result<int>.Exito(42);

        Assert.True(resultado.EsExito);
        Assert.Equal(42, resultado.Valor);
    }

    [Fact]
    public void ResultGenerico_FalloAlLeerValor_Lanza()
    {
        var resultado = Result<int>.Fallo(new Error("x", "y"));

        Assert.False(resultado.EsExito);
        var ex = Assert.Throws<InvalidOperationException>(() => resultado.Valor);
        Assert.Contains("x", ex.Message);
    }

    [Fact]
    public void ResultGenerico_DistingueExitoConCeroDeFallo()
    {
        var exitoConCero = Result<int>.Exito(0);
        var fallo = Result<int>.Fallo(new Error("x", "y"));

        Assert.True(exitoConCero.TryObtenerValor(out var valor));
        Assert.Equal(0, valor);
        Assert.False(fallo.TryObtenerValor(out _));
    }

    [Fact]
    public void ResultGenerico_PropagaErroresDeOtroResultado()
    {
        var origen = Result.Fallo(new Error("producto.no_encontrado", "No existe.", "Id"));
        var propagado = Result<decimal>.Fallo(origen);

        Assert.False(propagado.EsExito);
        Assert.Equal("producto.no_encontrado", propagado.Errores[0].Codigo);
        Assert.Equal("Id", propagado.Errores[0].Campo);
    }

    [Fact]
    public void ResultGenerico_PropagarDesdeExito_Lanza()
    {
        Assert.Throws<ArgumentException>(() => Result<int>.Fallo(Result.Exito()));
    }

    [Fact]
    public void Fallo_SinErrores_Lanza()
    {
        Assert.Throws<ArgumentException>(() => Result.Fallo());
    }

    [Fact]
    public void ResultGenerico_FalloSinErrores_Lanza()
    {
        Assert.Throws<ArgumentException>(() => Result<int>.Fallo());
    }
}
