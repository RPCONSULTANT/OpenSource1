extern alias BlazorApp;

using System.Net;
using BlazorApp::OpenSource1.Blazor.Navigation;
using BlazorApp::OpenSource1.Blazor.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenSource1.Application.Features.FacturasVenta.Borradores.Dtos;
using OpenSource1.Application.Features.FacturasVenta.Posteadas;
using OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Dtos;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.SmokeTests.TestInfrastructure;
using static OpenSource1.SmokeTests.TestInfrastructure.HtmlSsr;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Fix-Features A5/A6: inicio compacto sin hero azul y páginas de grupo con indicadores tolerantes a fallos.</summary>
public sealed class InicioYGruposTests
{
    private static readonly System.Text.RegularExpressions.Regex AnimacionEntrada =
        new("animate-(fade-in|slide-up|slide-up-slow|slide-in-left|pop|pulse-glow|float)\\b");

    [Fact]
    public async Task Inicio_SinHeroNiAnimaciones_ConKpisYTarjetasDeGrupo()
    {
        using var app = new BlazorSsrFactory();
        app.Simular<ISocioNegocioApiClient>().Setup(c => c.ListAllAsync(It.IsAny<SocioNegocioSearchFilter?>(), It.IsAny<CancellationToken>())).ReturnsAsync([new SocioNegocioResponse { NombreComercial = "A" }]);
        app.Simular<IProductoApiClient>().Setup(c => c.ListAllAsync(It.IsAny<ProductoSearchFilter?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ProductoResponse { Nombre = "P", Existencia = 0 }, new ProductoResponse { Nombre = "Q", Existencia = 3 }]);

        var html = await HtmlAsync(app.Cliente("Administrador"), "/");

        Assert.DoesNotContain("bg-gradient-to-br from-brand-700", html);
        Assert.DoesNotMatch("class=\"[^\"]*animate-(fade-in|slide-up|slide-in-left|pop|pulse-glow|float)", html.Split("<main")[1]);
        Assert.Contains("data-testid=\"kpis-inicio\"", html);
        Assert.Contains("href=\"/modulos/facturacion\"", html);
        Assert.Contains("href=\"/modulos/administracion\"", html);
    }

    [Fact]
    public void HomeRazor_SinHeroNiClasesDeAnimacion()
    {
        // SinFlashTests excluye Home.razor (A6): la cobertura del fuente del inicio vive aquí.
        var fuente = File.ReadAllText(Path.Combine(BlazorSsrFactory.RaizRepositorio(), "src", "OpenSource1.Blazor", "Components", "Pages", "Home.razor"));

        Assert.DoesNotContain("bg-gradient", fuente);
        Assert.DoesNotMatch(AnimacionEntrada, fuente);
        Assert.DoesNotContain("animate-", fuente);
    }

    [Fact]
    public async Task Inicio_Ejecutor_SoloGruposConModulosVisibles()
    {
        using var app = new BlazorSsrFactory();
        app.Simular<ISocioNegocioApiClient>().Setup(c => c.ListAllAsync(It.IsAny<SocioNegocioSearchFilter?>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        app.Simular<IProductoApiClient>().Setup(c => c.ListAllAsync(It.IsAny<ProductoSearchFilter?>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var html = await HtmlAsync(app.Cliente("Ejecutor"), "/");

        Assert.Contains("href=\"/modulos/clientes\"", html);
        Assert.DoesNotContain("href=\"/modulos/administracion\"", html);
    }

    [Fact]
    public async Task Inicio_SinCanConsult_SinKpisNiLlamadasALaApi()
    {
        using var app = new BlazorSsrFactory();
        var socios = app.Simular<ISocioNegocioApiClient>();
        var productos = app.Simular<IProductoApiClient>();

        // "CanModify" no concede consulta (permisos: "" no llega como cabecera: ver brief A5).
        var html = await HtmlAsync(app.Cliente("Administrador", permisos: "CanModify"), "/");

        Assert.DoesNotContain("data-testid=\"kpis-inicio\"", html);
        socios.Verify(c => c.ListAllAsync(It.IsAny<SocioNegocioSearchFilter?>(), It.IsAny<CancellationToken>()), Times.Never);
        productos.Verify(c => c.ListAllAsync(It.IsAny<ProductoSearchFilter?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Grupo_MuestraSusModulos_AbreSuGrupoEnElMenu_YGrupoInexistente404()
    {
        using var app = new BlazorSsrFactory();

        var html = await HtmlAsync(app.Cliente("Administrador"), "/modulos/configuracion");
        var inexistente = await app.Cliente("Administrador").GetAsync("/modulos/noexiste");

        Assert.Contains("href=\"/terminos-pago\"", html);
        Assert.Contains("href=\"/admin/fechas-registro\"", html);
        Assert.Matches("<details data-grupo=\"configuracion\" open", html);
        Assert.DoesNotMatch("<details data-grupo=\"clientes\" open", html);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    [Fact]
    public async Task Grupo_IndicadorQueFalla_SeOmite()
    {
        using var app = new BlazorSsrFactory();
        var facturas = app.Simular<IFacturaVentaApiClient>();
        facturas.Setup(c => c.ListBorradoresAsync(It.IsAny<FacturaVentaBorradorFiltro?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<FacturaVentaBorradorResponse>([], 1, 1, 7));
        facturas.Setup(c => c.ListFacturasAsync(It.IsAny<FacturaVentaSearchCriteria?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API caída"));
        app.Simular<INotaCreditoVentaApiClient>()
            .Setup(c => c.ListBorradoresAsync(It.IsAny<NotaCreditoVentaBorradorFiltro?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<NotaCreditoVentaBorradorResponse>([], 1, 1, 2));

        var html = await HtmlAsync(app.Cliente("Administrador"), "/modulos/facturacion");

        Assert.Contains("data-testid=\"indicadores-grupo\"", html);
        Assert.Contains("Borradores abiertos", html);
        Assert.Contains(">7<", html);
        Assert.Contains(">2<", html);
        Assert.Contains("Borradores de nota de crédito", html);
        Assert.DoesNotContain("Facturas posteadas", html);
        Assert.Contains("href=\"/facturas-venta\"", html);
        facturas.Verify(c => c.ListBorradoresAsync(It.Is<FacturaVentaBorradorFiltro?>(f => f != null && f.Estado == 1), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Indicadores_SinCanConsult_NingunoYSinLlamadas()
    {
        using var app = new BlazorSsrFactory();
        var facturas = app.Simular<IFacturaVentaApiClient>();
        _ = app.Cliente();

        using var scope = app.Services.CreateScope();
        var indicadores = scope.ServiceProvider.GetRequiredService<IIndicadoresModulos>();
        var resultado = await indicadores.ObtenerAsync("facturacion", PermisosTestAuthHandler.Principal("Administrador", "CanModify"));

        Assert.Empty(resultado);
        Assert.Empty(facturas.Invocations);
    }

    [Fact]
    public async Task Grupo_SinModulosVisibles_MensajeSinEnlaces()
    {
        using var app = new BlazorSsrFactory();

        var html = await HtmlAsync(app.Cliente("Ejecutor"), "/modulos/administracion");

        Assert.Contains("No tiene módulos disponibles en este grupo.", html);
        Assert.DoesNotContain("data-testid=\"tarjeta-modulo\"", html);
    }
}
