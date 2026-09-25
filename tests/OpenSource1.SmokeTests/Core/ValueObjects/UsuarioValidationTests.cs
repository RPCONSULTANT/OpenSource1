using OpenSource1.Core.Common;
using OpenSource1.Core.ValueObjects;

namespace OpenSource1.SmokeTests.Core.ValueObjects;

/// <summary>
/// Cubre la validación de datos de entrada de <see cref="Usuario"/>. La normalización de
/// valores válidos y la igualdad ya están cubiertas por
/// <c>OpenSource1.SmokeTests.UsuarioAndApplicationRolesTests</c> (UnitTest1.cs).
/// </summary>
public class UsuarioValidationTests
{
    [Fact]
    public void Constructor_ConUserNameVacio_LanzaErroresDeDominioConCampoUserName()
    {
        var excepcion = Assert.Throws<ErroresDeDominioException>(
            () => Usuario.Of("   ", "user@mail.com", "Nombre Completo"));

        var error = Assert.Single(excepcion.Errores);
        Assert.Equal("usuario.username_requerido", error.Codigo);
        Assert.Equal("userName", error.Campo);
    }

    [Fact]
    public void Constructor_ConEmailVacio_LanzaErroresDeDominioConCampoEmail()
    {
        var excepcion = Assert.Throws<ErroresDeDominioException>(
            () => Usuario.Of("rainiery", "   ", "Nombre Completo"));

        var error = Assert.Single(excepcion.Errores);
        Assert.Equal("usuario.email_requerido", error.Codigo);
        Assert.Equal("email", error.Campo);
    }

    [Fact]
    public void Constructor_ConFullNameVacio_LanzaErroresDeDominioConCampoFullName()
    {
        var excepcion = Assert.Throws<ErroresDeDominioException>(
            () => Usuario.Of("rainiery", "user@mail.com", "   "));

        var error = Assert.Single(excepcion.Errores);
        Assert.Equal("usuario.nombre_completo_requerido", error.Codigo);
        Assert.Equal("fullName", error.Campo);
    }

    [Fact]
    public void Constructor_ConNombresDeCampoExplicitos_UsanEsosNombresEnLugarDeLosParametros()
    {
        var excepcionUserName = Assert.Throws<ErroresDeDominioException>(
            () => Usuario.Of("   ", "user@mail.com", "Nombre Completo", "UserName", "Email", "FullName"));
        Assert.Equal("UserName", Assert.Single(excepcionUserName.Errores).Campo);

        var excepcionEmail = Assert.Throws<ErroresDeDominioException>(
            () => Usuario.Of("rainiery", "   ", "Nombre Completo", "UserName", "Email", "FullName"));
        Assert.Equal("Email", Assert.Single(excepcionEmail.Errores).Campo);

        var excepcionFullName = Assert.Throws<ErroresDeDominioException>(
            () => Usuario.Of("rainiery", "user@mail.com", "   ", "UserName", "Email", "FullName"));
        Assert.Equal("FullName", Assert.Single(excepcionFullName.Errores).Campo);
    }
}
