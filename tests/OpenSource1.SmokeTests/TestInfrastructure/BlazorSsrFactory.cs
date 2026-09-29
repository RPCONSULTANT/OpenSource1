extern alias BlazorApp;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace OpenSource1.SmokeTests.TestInfrastructure;

/// <summary>
/// Host real de OpenSource1.Blazor (pipeline, router, layout, antiforgery) sin API ni Postgres: los clientes tipados que la
/// prueba necesita se sustituyen por <see cref="Mock{T}"/>. Llamar a <see cref="Simular{T}"/> ANTES del primer
/// <see cref="Cliente"/> (el host se construye con la primera petición). Sin Docker.
/// </summary>
public sealed class BlazorSsrFactory : WebApplicationFactory<BlazorApp::Program>
{
    /// <summary>
    /// Dirección muerta (puerto 9, "discard") para <c>Api:BaseAddress</c>: un cliente tipado no simulado falla rápido en
    /// lugar de llamar a una API real que pudiera estar levantada en la máquina.
    /// </summary>
    public const string DireccionApiMuerta = "http://127.0.0.1:9/";

    private readonly Dictionary<Type, Mock> _simulados = [];
    private bool _iniciado;

    public Mock<T> Simular<T>() where T : class
    {
        if (_simulados.TryGetValue(typeof(T), out var existente))
        {
            return (Mock<T>)existente;
        }

        if (_iniciado)
        {
            throw new InvalidOperationException("Simular<T>() debe llamarse antes de crear el primer cliente.");
        }

        var mock = new Mock<T>();
        _simulados[typeof(T)] = mock;
        return mock;
    }

    public HttpClient Cliente(string roles = "Administrador", string? permisos = null, bool anonimo = false)
    {
        _iniciado = true;
        var cliente = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (anonimo)
        {
            cliente.DefaultRequestHeaders.Add(PermisosTestAuthHandler.CabeceraAnonimo, "true");
        }

        cliente.DefaultRequestHeaders.Add(PermisosTestAuthHandler.CabeceraRoles, roles);
        cliente.DefaultRequestHeaders.Add(PermisosTestAuthHandler.CabeceraPermisos, permisos ?? PermisosTestAuthHandler.PermisosDeRol(roles));
        return cliente;
    }

    /// <summary>
    /// Raíz del repositorio (carpeta que contiene <c>test.slnx</c>), subiendo desde <see cref="AppContext.BaseDirectory"/>.
    /// Helper único de TestInfrastructure: no duplicarlo en otros tests.
    /// </summary>
    public static string RaizRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "test.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException(
            $"No se encontró la raíz del repositorio (test.slnx) subiendo desde {AppContext.BaseDirectory}.");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Raíz de contenido explícita (wwwroot, appsettings del host): no depende del manifiesto de Mvc.Testing ni del alias.
        builder.UseContentRoot(Path.Combine(RaizRepositorio(), "src", "OpenSource1.Blazor"));
        // ApiClientOptions.SectionName = "Api": ningún cliente real puede alcanzar un stack levantado.
        builder.UseSetting("Api:BaseAddress", DireccionApiMuerta);
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(PermisosTestAuthHandler.Esquema)
                .AddScheme<AuthenticationSchemeOptions, PermisosTestAuthHandler>(PermisosTestAuthHandler.Esquema, _ => { });

            // Autentica con el esquema de prueba; el Challenge/Forbid siguen siendo los de la cookie real (302 a
            // /account/login y /access-denied), igual que en producción.
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = PermisosTestAuthHandler.Esquema;
                options.DefaultScheme = PermisosTestAuthHandler.Esquema;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultForbidScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            });
        });

        // ConfigureTestServices corre DESPUÉS de los registros de Program.cs: el mock reemplaza al AddHttpClient<…>.
        builder.ConfigureTestServices(services =>
        {
            foreach (var (tipo, mock) in _simulados)
            {
                services.RemoveAll(tipo);
                services.AddSingleton(tipo, mock.Object);
            }
        });
    }
}
