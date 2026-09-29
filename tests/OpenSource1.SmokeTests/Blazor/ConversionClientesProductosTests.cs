extern alias BlazorApp;

using System.Net;
using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Fix-Features B2c: clientes y productos con alta/edición en página-tarjeta, selección ?sel= y rutas antiguas redirigidas.</summary>
public sealed class ConversionClientesProductosTests
{
    private static readonly Guid IdCliente = Guid.Parse("7c000000-0000-0000-0000-000000000001");
    private static readonly Guid IdProducto = Guid.Parse("7c000000-0000-0000-0000-000000000002");
    private static readonly Guid IdAjeno = Guid.Parse("7c000000-0000-0000-0000-0000000000ff");

    [Theory]
    [InlineData("/clientes/new", "/clientes/nuevo")]
    [InlineData("/productos/new", "/productos/nuevo")]
    [InlineData("/clientes/new?returnUrl=%2Fclientes%3Fview%3Dlist", "/clientes/nuevo?returnUrl=%2Fclientes%3Fview%3Dlist")]
    [InlineData("/productos/new?returnUrl=%2Fproductos%3Fpagina%3D2", "/productos/nuevo?returnUrl=%2Fproductos%3Fpagina%3D2")]
    public async Task RutaNewAntigua_RedirigeANuevo(string origen, string destino)
    {
        using var app = Configurar(new BlazorSsrFactory());

        var respuesta = await app.Cliente().GetAsync(origen);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(destino, FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task FichaConEditTrue_RedirigeALaPaginaDeEdicion()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var cliente = await app.Cliente().GetAsync($"/clientes/{IdCliente}?edit=true");
        var producto = await app.Cliente().GetAsync($"/productos/{IdProducto}?edit=true");

        Assert.Equal($"/clientes/{IdCliente}/editar?returnUrl=%2Fclientes%2F{IdCliente}", FormulariosSsr.Destino(cliente));
        Assert.Equal($"/productos/{IdProducto}/editar?returnUrl=%2Fproductos%2F{IdProducto}", FormulariosSsr.Destino(producto));
    }

    [Theory]
    [InlineData("clientes", "7c000000-0000-0000-0000-000000000001", "cliente-edit")]
    [InlineData("productos", "7c000000-0000-0000-0000-000000000002", "producto-edit")]
    public async Task Ficha_SoloConsulta_EditarAbreLaPaginaDeEdicion(string ruta, string id, string formularioAntiguo)
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), $"/{ruta}/{id}");

        Assert.Contains($"href=\"/{ruta}/{id}/editar?returnUrl=%2F{ruta}%2F{id}\"", html);
        Assert.DoesNotContain(formularioAntiguo, html);
        Assert.DoesNotContain("edit=true", html);
    }

    [Theory]
    [InlineData("/clientes", "7c000000-0000-0000-0000-000000000001")]
    [InlineData("/productos", "7c000000-0000-0000-0000-000000000002")]
    public async Task ListadoConEditIdAntiguo_RedirigeAlEditorConRetornoALaLista(string lista, string id)
    {
        using var app = Configurar(new BlazorSsrFactory());

        var respuesta = await app.Cliente().GetAsync($"{lista}?view=list&editId={id}");

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        var destino = FormulariosSsr.Destino(respuesta);
        Assert.StartsWith($"{lista}/{id}/editar?returnUrl={Uri.EscapeDataString(lista)}", destino);
        Assert.DoesNotContain("editId", destino);
    }

    [Theory]
    [InlineData("/clientes/nuevo", "cliente-new")]
    [InlineData("/productos/nuevo", "producto-new")]
    public async Task Nuevo_TarjetaConSubidaDeImagen(string ruta, string formName)
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), ruta);

        Assert.Contains("data-testid=\"entity-form-page\"", html);
        Assert.Contains($"name=\"_handler\" value=\"{formName}\"", html);
        Assert.Contains("enctype=\"multipart/form-data\"", html);
        Assert.Contains("data-testid=\"limpiar\"", html);
    }

    [Fact]
    public async Task Editar_Cliente_CargaLoGuardado_YEjecutorNoPuede()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var admin = await HtmlSsr.HtmlAsync(app.Cliente(), $"/clientes/{IdCliente}/editar");
        var ejecutor = await HtmlSsr.HtmlAsync(app.Cliente("Ejecutor"), $"/clientes/{IdCliente}/editar");

        Assert.Contains("value=\"Comercial Uno\"", admin);
        Assert.Contains("name=\"_handler\" value=\"cliente-edit\"", admin);
        Assert.Contains("No tiene permiso para realizar esta acción.", ejecutor);
        Assert.Contains("value=\"cliente-edit\"", ejecutor);
    }

    [Fact]
    public async Task Editar_Producto_CargaLoGuardado_ConCancelarALaFicha()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), $"/productos/{IdProducto}/editar?returnUrl=%2Fproductos%2F{IdProducto}");

        Assert.Contains("value=\"Tornillo\"", html);
        Assert.Contains("name=\"_handler\" value=\"producto-edit\"", html);
        Assert.Contains($"href=\"/productos/{IdProducto}\" data-testid=\"cancelar\"", html);
    }

    [Fact]
    public async Task PostForzado_SinPermiso_DevuelveMensajeDeLaApi()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var socios = app.Simular<ISocioNegocioApiClient>();
        socios.Setup(c => c.UpdateAsync(IdCliente, It.IsAny<SocioNegocioInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SocioNegocioOperationResult(false, "No tiene permisos para modificar clientes (403)."));
        var ruta = $"/clientes/{IdCliente}/editar";

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente("Ejecutor"), ruta, "cliente-edit",
            new Dictionary<string, string> { ["Input.NombreComercial"] = "Forzado", ["Input.Email"] = "forzado@test.local" });
        var html = HtmlSsr.Decodificar(await respuesta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("No tiene permisos para modificar clientes (403).", html);
        socios.Verify(c => c.UpdateAsync(IdCliente, It.IsAny<SocioNegocioInput>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Guardar_Cliente_VuelveALaListaConOk()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var socios = app.Simular<ISocioNegocioApiClient>();
        socios.Setup(c => c.UpdateAsync(IdCliente, It.IsAny<SocioNegocioInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SocioNegocioOperationResult(true, "ok", IdCliente));
        var ruta = $"/clientes/{IdCliente}/editar?returnUrl=%2Fclientes%3Fview%3Dlist%26ok%3Dcreated";

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), ruta, "cliente-edit",
            new Dictionary<string, string> { ["Input.NombreComercial"] = "Comercial Dos", ["Input.Email"] = "dos@test.local" });

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/clientes?view=list&ok=updated", FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task Listados_ConBarraSeleccionYTarjetas()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var clientesLista = await HtmlSsr.HtmlAsync(app.Cliente(), $"/clientes?view=list&sel={IdCliente}");
        var clientesTarjetas = await HtmlSsr.HtmlAsync(app.Cliente(), "/clientes?view=grid");
        var productosLista = await HtmlSsr.HtmlAsync(app.Cliente(), $"/productos?view=list&sel={IdProducto}");
        var productosTarjetas = await HtmlSsr.HtmlAsync(app.Cliente(), "/productos?view=grid");

        Assert.Contains($"<a data-testid=\"accion-editar\" href=\"/clientes/{IdCliente}/editar?returnUrl=", clientesLista);
        Assert.Contains("href=\"/clientes/nuevo?returnUrl=", clientesLista);
        Assert.Contains("data-testid=\"seleccionar-fila\"", clientesLista);
        Assert.Contains("Detalles", clientesLista);
        Assert.Contains("data-testid=\"tarjeta-acciones\"", clientesTarjetas);
        Assert.Contains($"<a data-testid=\"accion-editar\" href=\"/productos/{IdProducto}/editar?returnUrl=", productosLista);
        Assert.Contains("href=\"/productos/nuevo?returnUrl=", productosLista);
        Assert.Contains("data-testid=\"tarjeta-acciones\"", productosTarjetas);
        Assert.DoesNotContain("href=\"/clientes/new\"", clientesLista);
        Assert.DoesNotContain("href=\"/productos/new\"", productosLista);
        Assert.DoesNotContain("focusId", clientesLista + productosLista);
        Assert.DoesNotContain("edit=true", clientesLista + clientesTarjetas + productosLista + productosTarjetas);
    }

    [Fact]
    public async Task SeleccionQueNoEstaEnLaPagina_DeshabilitaAcciones()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), $"/clientes?view=list&sel={IdAjeno}");

        Assert.Contains("<span data-testid=\"accion-editar\" aria-disabled=\"true\"", html);
        Assert.Contains("<span data-testid=\"accion-eliminar\" aria-disabled=\"true\"", html);
        Assert.DoesNotContain($"/clientes/{IdAjeno}/editar", html);
        Assert.DoesNotContain("Detalle visual del registro seleccionado.", html);
    }

    [Fact]
    public async Task Tarjetas_Clientes_SinContenedorRecortado()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), "/clientes?view=grid");

        // R15: el menú ⋯ de TarjetaAcciones se abre hacia arriba; ni la tarjeta del listado (desde su apertura, justo antes del
        // formulario de búsqueda) ni la tarjeta de cada cliente pueden llevar overflow-hidden hasta las acciones.
        var busqueda = html.IndexOf("<form method=\"get\" action=\"/clientes\"", StringComparison.Ordinal);
        var tarjetaListado = html.LastIndexOf("<div", busqueda, StringComparison.Ordinal);
        var acciones = html.IndexOf("data-testid=\"tarjeta-acciones\"", busqueda, StringComparison.Ordinal);
        Assert.True(tarjetaListado > 0 && acciones > tarjetaListado);
        Assert.DoesNotContain("overflow-hidden", html[tarjetaListado..acciones]);
    }

    [Theory]
    [InlineData("/clientes?view=list&focusId={0}", "/clientes?view=list&sel={0}", "cliente")]
    [InlineData("/productos?focusId={0}", "/productos?sel={0}", "producto")]
    [InlineData("/productos?focusId={0}&ok=updated", "/productos?sel={0}&ok=updated", "producto")]
    public async Task FocusIdAntiguo_SeMapeaASel(string origen, string destino, string entidad)
    {
        using var app = Configurar(new BlazorSsrFactory());
        var id = entidad == "cliente" ? IdCliente : IdProducto;

        var respuesta = await app.Cliente().GetAsync(string.Format(origen, id));

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(string.Format(destino, id), FormulariosSsr.Destino(respuesta));
    }

    [Theory]
    [InlineData("/clientes", "cliente")]
    [InlineData("/productos", "producto")]
    public async Task UrlActual_NoArrastraReporteNiPanelDeFiltros(string lista, string entidad)
    {
        using var app = Configurar(new BlazorSsrFactory());
        var id = entidad == "cliente" ? IdCliente : IdProducto;

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), $"{lista}?showFilters=true&report=true&sel={id}");

        var retorno = Uri.EscapeDataString($"{lista}?sel={id}");
        Assert.Contains($"<a data-testid=\"accion-editar\" href=\"{lista}/{id}/editar?returnUrl={retorno}\"", html);
        Assert.Contains($"href=\"{lista}/nuevo?returnUrl={retorno}\" data-testid=\"accion-nuevo\"", html);
    }

    [Fact]
    public async Task SubtituloDelCodigo_SoloEnElAlta()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var alta = await HtmlSsr.HtmlAsync(app.Cliente(), "/clientes/nuevo");
        var edicion = await HtmlSsr.HtmlAsync(app.Cliente(), $"/clientes/{IdCliente}/editar");

        Assert.Contains("El código lo asigna el sistema al guardar.", alta);
        Assert.DoesNotContain("El código lo asigna el sistema", edicion);
    }

    [Fact]
    public async Task Alta_Cliente_VuelveALaListaConOkCreated()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var socios = app.Simular<ISocioNegocioApiClient>();
        socios.Setup(c => c.CreateAsync(It.Is<SocioNegocioInput>(i => i.NombreComercial == "Comercial Dos"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SocioNegocioOperationResult(true, "ok", Guid.NewGuid()));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), "/clientes/nuevo?returnUrl=%2Fclientes%3Fview%3Dlist", "cliente-new",
            new Dictionary<string, string> { ["Input.NombreComercial"] = "Comercial Dos", ["Input.Email"] = "dos@test.local" });
        var destino = FormulariosSsr.Destino(respuesta);
        var lista = await HtmlSsr.HtmlAsync(app.Cliente(), destino);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/clientes?view=list&ok=created", destino);
        Assert.Contains("Cliente creado satisfactoriamente.", lista);
        socios.Verify(c => c.CreateAsync(It.IsAny<SocioNegocioInput>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static BlazorSsrFactory Configurar(BlazorSsrFactory app)
    {
        var cliente = new SocioNegocioResponse { Id = IdCliente, Codigo = "C0001", NombreComercial = "Comercial Uno", Email = "uno@test.local" };
        var producto = new ProductoResponse { Id = IdProducto, Codigo = "P0001", Nombre = "Tornillo", CategoriaCodigo = "GENERAL", CategoriaNombre = "General" };
        var socios = app.Simular<ISocioNegocioApiClient>();
        socios.Setup(c => c.ListAsync(It.IsAny<SocioNegocioSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<SocioNegocioResponse>([cliente], 1, 50, 1));
        socios.Setup(c => c.GetByIdAsync(IdCliente, It.IsAny<CancellationToken>())).ReturnsAsync(cliente);
        socios.Setup(c => c.ListAllAsync(It.IsAny<SocioNegocioSearchFilter?>(), It.IsAny<CancellationToken>())).ReturnsAsync([cliente]);
        var productos = app.Simular<IProductoApiClient>();
        productos.Setup(c => c.ListAsync(It.IsAny<ProductoSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ProductoResponse>([producto], 1, 50, 1));
        productos.Setup(c => c.GetByIdAsync(IdProducto, It.IsAny<CancellationToken>())).ReturnsAsync(producto);
        productos.Setup(c => c.ListAllAsync(It.IsAny<ProductoSearchFilter?>(), It.IsAny<CancellationToken>())).ReturnsAsync([producto]);
        app.Simular<ITerminoPagoApiClient>().Setup(c => c.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        app.Simular<IGrupoContableApiClient>();
        app.Simular<IGrupoClienteContableApiClient>();
        app.Simular<ICategoriaProductoApiClient>().Setup(c => c.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        // Sin configurar, Moq devuelve null para la lista de unidades (el cliente real nunca lo hace).
        app.Simular<IUnidadMedidaApiClient>().Setup(c => c.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        app.Simular<ICobroApiClient>();
        return app;
    }
}
