extern alias BlazorApp;

using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>
/// "/uploads" sirve contenido privado (avatares, imágenes de clientes y productos): solo para usuarios autenticados. Estos
/// tests golpean el pipeline real de <c>OpenSource1.Blazor</c> (no una reimplementación) y comprueban que una petición
/// anónima nunca recibe el fichero (se le redirige a /account/login, igual que cualquier página protegida) y que una
/// petición autenticada sí lo recibe, con "X-Content-Type-Options: nosniff" y "Cache-Control: private, no-store".
///
/// El host de Blazor no necesita la API real ni Postgres para arrancar: solo los necesita en tiempo de petición, cuando una
/// página llama a alguno de los <c>IHttpClient</c> tipados. Como esta prueba solo golpea el middleware de ficheros estáticos
/// de /uploads (que corta la petición antes de llegar a ninguna página), basta con un <see cref="WebApplicationFactory{T}"/>
/// sobre <c>BlazorApp::Program</c> con un content root temporal — sin Docker.
///
/// Para autenticar sin la API real se reutiliza <see cref="TestAuthHandler"/> (el mismo esquema de prueba que usan los
/// tests de OpenSource1.Api) como esquema de autenticación por defecto: sin la cabecera "X-Test-Anonymous" autentica;
/// con ella, no. El middleware bajo prueba solo exige <c>User.Identity.IsAuthenticated</c> (nunca CanConsult ni ningún otro
/// permiso: el avatar del propio usuario debe poder verse siempre), así que no hace falta simular claims de permiso, y el
/// Challenge que dispara sigue siendo el del esquema de cookies real configurado en Program.cs (mismo LoginPath que usan
/// las páginas).
/// </summary>
public sealed class UploadsAuthorizationTests : IDisposable
{
    private const string FileName = "cliente-0123456789abcdef0123456789abcdef.png";
    private static readonly byte[] FileBytes = [1, 2, 3, 4];

    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), "ax-blazor-uploads-" + Guid.NewGuid().ToString("N"));
    private readonly UploadsFactory _factory;

    public UploadsAuthorizationTests()
    {
        Directory.CreateDirectory(Path.Combine(_contentRoot, "storage", "uploads", "clientes"));
        Directory.CreateDirectory(Path.Combine(_contentRoot, "wwwroot"));
        File.WriteAllBytes(Path.Combine(_contentRoot, "storage", "uploads", "clientes", FileName), FileBytes);

        _factory = new UploadsFactory(_contentRoot);
    }

    public void Dispose()
    {
        _factory.Dispose();
        try { Directory.Delete(_contentRoot, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task Anonimo_NoRecibeElFichero_SinoQueEsRedirigidoALogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var request = new HttpRequestMessage(HttpMethod.Get, $"/uploads/clientes/{FileName}");
        request.Headers.Add("X-Test-Anonymous", "true");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith("/account/login", response.Headers.Location!.PathAndQuery);

        var body = await response.Content.ReadAsByteArrayAsync();
        Assert.Empty(body);
    }

    [Fact]
    public async Task Autenticado_RecibeElFichero_ConNosniffYCacheControlPrivado()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var request = new HttpRequestMessage(HttpMethod.Get, $"/uploads/clientes/{FileName}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(FileBytes, await response.Content.ReadAsByteArrayAsync());

        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var nosniff));
        Assert.Equal("nosniff", Assert.Single(nosniff!));

        Assert.NotNull(response.Headers.CacheControl);
        Assert.True(response.Headers.CacheControl!.Private);
        Assert.True(response.Headers.CacheControl.NoStore);
    }

    private sealed class UploadsFactory(string contentRoot) : WebApplicationFactory<BlazorApp::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseContentRoot(contentRoot);
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

                // Solo cambia qué esquema decide `User.Identity.IsAuthenticated` (el que evalúa el middleware bajo
                // prueba); el esquema "Cookies" configurado en Program.cs sigue registrado tal cual, así que el
                // ChallengeAsync(CookieAuthenticationDefaults.AuthenticationScheme) del middleware sigue siendo el real.
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultScheme = "Test";
                });
            });
        }
    }
}
