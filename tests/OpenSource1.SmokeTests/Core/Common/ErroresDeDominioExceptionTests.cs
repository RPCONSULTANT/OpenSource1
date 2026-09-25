using OpenSource1.Core.Common;

namespace OpenSource1.SmokeTests.Core.Common;

public class ErroresDeDominioExceptionTests
{
    [Fact]
    public void ConstructorConError_ExponeUnSoloError()
    {
        var error = new Error("filtro.valor_invalido", "El valor 'abc' no es válido para el filtro 'Precio'.", "Precio");

        var excepcion = new ErroresDeDominioException(error);

        var errorExpuesto = Assert.Single(excepcion.Errores);
        Assert.Equal(error, errorExpuesto);
    }

    [Fact]
    public void ConstructorConLista_ConservaTodosLosErrores()
    {
        var errores = new[]
        {
            new Error("cliente.pais_invalido", "El país 'XX' no existe.", "PaisCodigo"),
            new Error("cliente.email_invalido", "El email no es válido.", "Email"),
        };

        var excepcion = new ErroresDeDominioException(errores);

        Assert.Equal(2, excepcion.Errores.Count);
        Assert.Equal(errores[0], excepcion.Errores[0]);
        Assert.Equal(errores[1], excepcion.Errores[1]);
    }

    [Fact]
    public void Message_UneLosMensajesDeTodosLosErrores()
    {
        var errores = new[]
        {
            new Error("a", "Primer mensaje."),
            new Error("b", "Segundo mensaje."),
        };

        var excepcion = new ErroresDeDominioException(errores);

        Assert.Equal("Primer mensaje.; Segundo mensaje.", excepcion.Message);
    }

    [Fact]
    public void ConstructorConListaVacia_Lanza()
    {
        Assert.Throws<ArgumentException>(() => new ErroresDeDominioException([]));
    }
}
