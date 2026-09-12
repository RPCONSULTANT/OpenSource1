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
    public void ResultGenerico_FalloNoTieneValor()
    {
        var resultado = Result<int>.Fallo(new Error("x", "y"));

        Assert.False(resultado.EsExito);
        Assert.Equal(default, resultado.Valor);
    }

    [Fact]
    public void Fallo_SinErrores_Lanza()
    {
        Assert.Throws<ArgumentException>(() => Result.Fallo());
    }
}
