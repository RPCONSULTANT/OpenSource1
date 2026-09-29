extern alias BlazorApp;

using System.Net;
using BlazorApp::OpenSource1.Blazor.Navigation;
using BlazorApp::OpenSource1.Blazor.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenSource1.Application.Security;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>
/// Task A1: la fábrica SSR del host Blazor arranca sin API ni Postgres, apunta los clientes tipados a una dirección muerta
/// (ruling R3), registra <see cref="IRegistroModulos"/> y conserva el Challenge real de la cookie para anónimos.
/// </summary>
public sealed class BlazorSsrFactoryTests : IDisposable
{
    private readonly BlazorSsrFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public void ApiBaseAddress_EsLaDireccionMuerta_YElRegistroEstaRegistrado()
    {
        _ = _factory.Cliente();
        using var scope = _factory.Services.CreateScope();

        var opciones = scope.ServiceProvider.GetRequiredService<IOptions<ApiClientOptions>>().Value;

        Assert.Equal(new Uri(BlazorSsrFactory.DireccionApiMuerta), opciones.BaseAddress);
        Assert.IsType<RegistroModulos>(scope.ServiceProvider.GetRequiredService<IRegistroModulos>());
    }

    [Fact]
    public async Task Anonimo_EnPaginaProtegida_RedirigeALogin()
    {
        var cliente = _factory.Cliente(anonimo: true);

        var respuesta = await cliente.GetAsync("/clientes");

        Assert.Equal(HttpStatusCode.Found, respuesta.StatusCode);
        Assert.StartsWith("/account/login", FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task PermisosVacios_SignificaSinNingunPermiso_RedirigeAAccessDenied()
    {
        // permisos: "" = cero permisos (no los del rol): Supervisor sin CanConsult no entra a /clientes.
        var respuesta = await _factory.Cliente("Supervisor", permisos: "").GetAsync("/clientes");

        Assert.Equal(HttpStatusCode.Found, respuesta.StatusCode);
        Assert.StartsWith("/access-denied", FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task PermisosNulos_UsaLosPermisosDelRol()
    {
        var respuesta = await _factory.Cliente("Supervisor").GetAsync("/modulos/clientes");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public void Simular_TrasConstruirElHost_Lanza()
    {
        // El host puede construirse sin Cliente() (p. ej. al leer Services): un Simular<T>() posterior no tendría efecto.
        _ = _factory.Services;

        Assert.Throws<InvalidOperationException>(() => _factory.Simular<IRegistroModulos>());
    }

    [Fact]
    public void RaizRepositorio_ContieneLaSolucion()
    {
        Assert.True(File.Exists(Path.Combine(BlazorSsrFactory.RaizRepositorio(), "test.slnx")));
    }

    [Theory]
    [InlineData("Administrador", true)]
    [InlineData("Supervisor", false)]
    public async Task Renderizador_AplicaLasPoliticasRealesAlUsuarioEnCascada(string rol, bool ve)
    {
        RenderFragment<AuthenticationState> autorizado = _ => b => b.AddContent(0, "zona-admin");
        var html = await RenderizadorComponentes.RenderizarAsync<AuthorizeView>(
            PermisosTestAuthHandler.Principal(rol),
            new Dictionary<string, object?> { ["Policy"] = ApplicationPolicies.CanAdministrar, ["Authorized"] = autorizado });

        Assert.Equal(ve, html.Contains("zona-admin", StringComparison.Ordinal));
    }

    [Fact]
    public void Decodificar_ResuelveEntidadesDeAcentos()
    {
        Assert.Equal("Facturación", HtmlSsr.Decodificar("Facturaci&#xF3;n"));
    }
}
