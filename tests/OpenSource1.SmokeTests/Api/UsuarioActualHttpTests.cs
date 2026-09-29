using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenSource1.Api.Infrastructure;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

/// <summary><see cref="UsuarioActualHttp"/> (claims del JWT) y su registro en la API por encima del <c>UsuarioActualSistema</c>.</summary>
[Collection(PostgresCollection.Name)]
public sealed class UsuarioActualHttpTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    [Fact]
    public void ConUsuarioAutenticado_TomaNombreEIdDeLosClaims()
    {
        var id = Guid.NewGuid();
        var contexto = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "ana"), new Claim(ClaimTypes.NameIdentifier, id.ToString())], "Test")),
        };

        var usuario = new UsuarioActualHttp(new HttpContextAccessor { HttpContext = contexto });

        Assert.Equal("ana", usuario.Nombre);
        Assert.Equal(id, usuario.Id);
    }

    [Fact]
    public void SinPeticionOSinIdGuid_EsSystemSinId()
    {
        var sinPeticion = new UsuarioActualHttp(new HttpContextAccessor());
        var idNoGuid = new UsuarioActualHttp(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "no-es-guid")], "Test")),
            },
        });

        Assert.Equal(("system", (Guid?)null), (sinPeticion.Nombre, sinPeticion.Id));
        Assert.Equal(("system", (Guid?)null), (idNoGuid.Nombre, idNoGuid.Id));
    }

    [Fact]
    public async Task LaApi_ResuelveUsuarioActualHttp()
    {
        await using var factory = fixture.CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();

        Assert.IsType<UsuarioActualHttp>(scope.ServiceProvider.GetRequiredService<IUsuarioActual>());
    }
}
