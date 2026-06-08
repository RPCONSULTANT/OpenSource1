using OpenSource1.Application.Security;
using OpenSource1.Core.ValueObjects;

namespace OpenSource1.SmokeTests;

public class UsuarioAndApplicationRolesTests
{
    [Fact]
    public void Usuario_Constructor_NormalizesValues()
    {
        var usuario = new Usuario("  rainiery  ", " USER@MAIL.COM ", "  Rainiery Penia  ");

        Assert.Equal("rainiery", usuario.UserName);
        Assert.Equal("user@mail.com", usuario.Email);
        Assert.Equal("Rainiery Penia", usuario.FullName);
    }

    [Fact]
    public void Usuario_Equality_IsCaseInsensitiveForUserName()
    {
        var first = new Usuario("rainiery", "user@mail.com", "Rainiery Penia");
        var second = new Usuario("RAINIERY", "user@mail.com", "Rainiery Penia");

        Assert.Equal(first, second);
    }

    [Fact]
    public void ApplicationRoles_All_ContainsThreeExpectedRoles()
    {
        Assert.Equal(3, ApplicationRoles.All.Length);
        Assert.Contains(ApplicationRoles.Administrator, ApplicationRoles.All);
        Assert.Contains(ApplicationRoles.Supervisor, ApplicationRoles.All);
        Assert.Contains(ApplicationRoles.Executor, ApplicationRoles.All);
    }
}
