extern alias BlazorApp;

using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.Busqueda.Dtos;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>
/// Fix-Features A4: la paleta es JS progresivo; aquí se prueba lo que la sostiene en el servidor: la isla JSON con SOLO los
/// módulos visibles, el marcado accesible y el endpoint /buscar/sugerencias (el JWT nunca sale del servidor). El
/// comportamiento de teclado se prueba en el E2E (tests/e2e/specs/paleta.spec.ts, Task D3).
/// </summary>
public sealed class PaletaBusquedaTests
{
    [Theory]
    [InlineData("Ejecutor", false)]
    [InlineData("Administrador", true)]
    public async Task Isla_SoloModulosVisibles(string rol, bool veUsuarios)
    {
        using var app = new BlazorSsrFactory();
        var html = await (await app.Cliente(rol).GetAsync("/reporteria")).Content.ReadAsStringAsync();

        var isla = Regex.Match(html, "<script type=\"application/json\" id=\"modulos-data\">(.*?)</script>", RegexOptions.Singleline);
        Assert.True(isla.Success, "Falta la isla #modulos-data.");
        Assert.DoesNotContain("<", isla.Groups[1].Value);
        var rutas = JsonDocument.Parse(isla.Groups[1].Value).RootElement.EnumerateArray().Select(m => m.GetProperty("ruta").GetString()).ToList();

        Assert.Contains("/clientes", rutas);
        Assert.Equal(veUsuarios, rutas.Contains("/admin/users"));
        Assert.Equal(veUsuarios, rutas.Contains("/admin/fechas-registro"));
    }

    [Fact]
    public async Task Marcado_Accesible_YScriptDeLaPaleta()
    {
        using var app = new BlazorSsrFactory();
        var html = await (await app.Cliente().GetAsync("/reporteria")).Content.ReadAsStringAsync();

        Assert.Contains("role=\"combobox\"", html);
        Assert.Contains("aria-controls=\"busqueda-paleta-lista\"", html);
        Assert.Matches(new Regex("<div id=\"busqueda-paleta\" role=\"dialog\"[^>]*hidden"), html);
        Assert.Contains("role=\"listbox\"", html);
        Assert.Matches(new Regex("<script src=\"[^\"]*app\\.search[^\"]*\\.js\"></script>"), html);
    }

    [Fact]
    public async Task Sugerencias_DevuelveElJsonDeLaApi()
    {
        using var app = new BlazorSsrFactory();
        app.Simular<IBusquedaApiClient>()
            .Setup(c => c.BuscarAsync("torn", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConsultaResultado<BusquedaGlobalResponse>(true, string.Empty, new BusquedaGlobalResponse(
                [new GrupoResultadosBusqueda("productos", "Productos", [new ResultadoBusqueda("productos", "1", "Tornillo", "T1", "/productos/1")])])));

        var respuesta = await app.Cliente().GetAsync("/buscar/sugerencias?q=%20torn%20");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var raiz = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("/productos/1", raiz.GetProperty("grupos")[0].GetProperty("items")[0].GetProperty("ruta").GetString());
    }

    [Fact]
    public async Task Sugerencias_QCorta_400_Anonimo_Login_SinCanConsult_Prohibido()
    {
        using var app = new BlazorSsrFactory();
        var api = app.Simular<IBusquedaApiClient>();

        Assert.Equal(HttpStatusCode.BadRequest, (await app.Cliente().GetAsync("/buscar/sugerencias?q=a")).StatusCode);

        var anonimo = await app.Cliente(anonimo: true).GetAsync("/buscar/sugerencias?q=abc");
        Assert.Equal(HttpStatusCode.Redirect, anonimo.StatusCode);
        Assert.StartsWith("/account/login", FormulariosSsr.Destino(anonimo));

        var sinPermiso = await app.Cliente("Supervisor", permisos: "CanModify").GetAsync("/buscar/sugerencias?q=abc");
        Assert.Equal(HttpStatusCode.Redirect, sinPermiso.StatusCode);
        Assert.StartsWith("/access-denied", FormulariosSsr.Destino(sinPermiso));

        var ningunPermiso = await app.Cliente("Supervisor", permisos: "").GetAsync("/buscar/sugerencias?q=abc");
        Assert.Equal(HttpStatusCode.Redirect, ningunPermiso.StatusCode);
        Assert.StartsWith("/access-denied", FormulariosSsr.Destino(ningunPermiso));

        // Ni la consulta corta ni los rechazos de autorización llegan a llamar a la API.
        api.Verify(c => c.BuscarAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Script_NoDuplicaListenersTrasNavegacionMejorada()
    {
        // La sincronización de atributos de la navegación mejorada de Blazor borra los data-* que no vienen del servidor,
        // pero conserva el <input> del layout con sus listeners: el "ya cableado" debe vivir en JS (WeakSet), no en el DOM.
        var fuente = File.ReadAllText(Path.Combine(BlazorSsrFactory.RaizRepositorio(), "src", "OpenSource1.Blazor", "wwwroot", "app.search.js"));

        Assert.Contains("new WeakSet()", fuente);
        Assert.Matches(new Regex(@"if \(!el \|\| cableados\.has\(el\.input\)\) return;\s*cableados\.add\(el\.input\);"), fuente);
        Assert.DoesNotContain("dataset.", fuente);
        Assert.Contains("window.Blazor.addEventListener('enhancedload'", fuente);
    }

    [Fact]
    public void Script_EncabezadosDeGrupo_SonGruposConEtiqueta()
    {
        // Dentro del listbox cada sección es role="group" con aria-label y contiene sus opciones; el título visible no se
        // repite al lector de pantalla (aria-hidden) y nada del listbox usa role="presentation".
        var fuente = File.ReadAllText(Path.Combine(BlazorSsrFactory.RaizRepositorio(), "src", "OpenSource1.Blazor", "wwwroot", "app.search.js"));
        var funcion = Regex.Match(fuente, @"function grupo\(lista, texto\) \{(?<cuerpo>.*?)\n  \}", RegexOptions.Singleline).Groups["cuerpo"].Value;

        Assert.Contains("setAttribute('role', 'group')", funcion);
        Assert.Contains("setAttribute('aria-label', texto)", funcion);
        Assert.Contains("setAttribute('aria-hidden', 'true')", funcion);
        Assert.DoesNotContain("'presentation'", fuente);
    }

    [Fact]
    public async Task Sugerencias_ApiCaida_502()
    {
        using var app = new BlazorSsrFactory();
        var api = app.Simular<IBusquedaApiClient>();
        api.Setup(c => c.BuscarAsync("caida", 5, It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("sin API"));
        api.Setup(c => c.BuscarAsync("fallo", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConsultaResultado<BusquedaGlobalResponse>(false, "No fue posible cargar los resultados de la búsqueda."));

        Assert.Equal(HttpStatusCode.BadGateway, (await app.Cliente().GetAsync("/buscar/sugerencias?q=caida")).StatusCode);
        Assert.Equal(HttpStatusCode.BadGateway, (await app.Cliente().GetAsync("/buscar/sugerencias?q=fallo")).StatusCode);
    }
}
