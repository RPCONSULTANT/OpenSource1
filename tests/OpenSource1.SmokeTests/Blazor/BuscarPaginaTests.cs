extern alias BlazorApp;

using BlazorApp::OpenSource1.Blazor.Components.Pages;
using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.Busqueda.Dtos;
using OpenSource1.SmokeTests.TestInfrastructure;
using static OpenSource1.SmokeTests.TestInfrastructure.HtmlSsr;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Fix-Features A3: página /buscar (módulos del registro + un bloque por tipo) y barra superior de búsqueda.</summary>
public sealed class BuscarPaginaTests
{
    [Fact]
    public async Task Buscar_MuestraRegistrosConEnlace_YVerTodos()
    {
        using var app = new BlazorSsrFactory();
        var productoId = Guid.NewGuid();
        app.Simular<IBusquedaApiClient>()
            .Setup(c => c.BuscarAsync("tornillo", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Respuesta(new ResultadoBusqueda(TiposResultadoBusqueda.Productos, productoId.ToString(), "Tornillo 3/8", "TOR-38", $"/productos/{productoId}")));

        var html = await HtmlAsync(app.Cliente(), "/buscar?q=tornillo");

        Assert.Contains($"href=\"/productos/{productoId}\"", html);
        Assert.Contains("Tornillo 3/8", html);
        Assert.Contains("href=\"/productos?nombre=tornillo\"", html);
        Assert.Contains("Ningún módulo coincide", html);
        Assert.DoesNotContain("data-testid=\"resultados-clientes\"", html);
    }

    [Fact]
    public async Task Buscar_ModulosSinAcentos_SegunPermisos()
    {
        using var app = new BlazorSsrFactory();
        app.Simular<IBusquedaApiClient>()
            .Setup(c => c.BuscarAsync(It.IsAny<string>(), 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Respuesta());

        var admin = await HtmlAsync(app.Cliente("Administrador"), "/buscar?q=categoria");
        var ejecutor = await HtmlAsync(app.Cliente("Ejecutor"), "/buscar?q=usuarios");
        var adminUsuarios = await HtmlAsync(app.Cliente("Administrador"), "/buscar?q=usuarios");

        Assert.Contains("href=\"/categorias-producto\"", admin);
        // Solo el bloque de resultados (el menú lateral ya se probó en A2).
        Assert.DoesNotContain("href=\"/admin/users\"", BloqueModulos(ejecutor));
        Assert.Contains("href=\"/admin/users\"", BloqueModulos(adminUsuarios));
    }

    [Fact]
    public async Task Buscar_ConApiCaida_MuestraModulosYAviso()
    {
        using var app = new BlazorSsrFactory();
        app.Simular<IBusquedaApiClient>()
            .Setup(c => c.BuscarAsync(It.IsAny<string>(), 5, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API caída"));

        var html = await HtmlAsync(app.Cliente(), "/buscar?q=clientes");

        Assert.Contains("No fue posible buscar registros en este momento", html);
        Assert.Contains("href=\"/clientes\"", BloqueModulos(html));
    }

    [Fact]
    public async Task Buscar_ConsultaCorta_NoLlamaALaApi()
    {
        using var app = new BlazorSsrFactory();
        var api = app.Simular<IBusquedaApiClient>();

        var html = await HtmlAsync(app.Cliente(), "/buscar?q=%20a%20");

        Assert.Contains("Escriba al menos 2 caracteres", html);
        api.Verify(c => c.BuscarAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BarraSuperior_EnPaginasAutenticadas_YConservaLaConsulta()
    {
        using var app = new BlazorSsrFactory();
        app.Simular<IBusquedaApiClient>()
            .Setup(c => c.BuscarAsync(It.IsAny<string>(), 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Respuesta());

        var reporteria = await HtmlAsync(app.Cliente(), "/reporteria");
        var buscar = await HtmlAsync(app.Cliente(), "/buscar?q=abc");
        var login = await app.Cliente(anonimo: true).GetStringAsync("/account/login");

        Assert.Contains("data-testid=\"busqueda-global-form\"", reporteria);
        Assert.Contains("action=\"/buscar\"", reporteria);
        Assert.Contains("id=\"busqueda-global\" name=\"q\"", reporteria);
        Assert.Contains("value=\"abc\"", buscar);
        Assert.DoesNotContain("data-testid=\"busqueda-global-form\"", login);
    }

    [Theory]
    [InlineData(TiposResultadoBusqueda.Clientes, "ana", "/clientes?nombre=ana")]
    [InlineData(TiposResultadoBusqueda.Productos, "tor", "/productos?nombre=tor")]
    [InlineData(TiposResultadoBusqueda.Facturas, "FV01", "/facturas-venta?numero=FV01")]
    [InlineData(TiposResultadoBusqueda.Facturas, "comercial", "/facturas-venta?nombre=comercial")]
    [InlineData(TiposResultadoBusqueda.BorradoresFactura, "B 1", "/facturas-venta/borradores?numero=B%201")]
    [InlineData(TiposResultadoBusqueda.NotasCredito, "NC", "/notas-credito-venta?numero=NC")]
    [InlineData(TiposResultadoBusqueda.BorradoresNotaCredito, "NC", "/notas-credito-venta/borradores?numero=NC")]
    public void VerTodosUrl_ApuntaAlListadoFiltrado(string tipo, string q, string esperado)
    {
        Assert.Equal(esperado, Buscar.VerTodosUrl(tipo, q));
    }

    /// <summary>Desde la sección de módulos hasta el final (excluye el menú lateral, que va antes en el layout).</summary>
    private static string BloqueModulos(string html)
    {
        var inicio = html.IndexOf("data-testid=\"resultados-modulos\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Falta el bloque data-testid=\"resultados-modulos\".");
        return html[inicio..];
    }

    private static ConsultaResultado<BusquedaGlobalResponse> Respuesta(params ResultadoBusqueda[] items) =>
        new(true, string.Empty, new BusquedaGlobalResponse(
            items.GroupBy(i => i.Tipo).Select(g => new GrupoResultadosBusqueda(g.Key, g.Key, g.ToList())).ToList()));
}
