extern alias BlazorApp;

using System.Net;
using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;
using static OpenSource1.SmokeTests.TestInfrastructure.HtmlSsr;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>
/// Ola final de Fix-Features (Minor 1): en los listados y fichas de maestros el formulario de borrado está SIEMPRE en el árbol
/// (EditForm vacío con el mismo FormName cuando el diálogo no se muestra). Un POST de borrado forzado sin CanDelete llega al
/// handler y devuelve el mensaje real de la API, no el 400 genérico de Blazor.
/// </summary>
public sealed class BorradoForzadoTests
{
    private static readonly Guid Id = Guid.Parse("7f000000-0000-0000-0000-000000000001");
    private const string MensajeApi = "La API rechazó el borrado forzado (403 de prueba).";

    [Theory]
    [InlineData("/almacenes", "delete-almacen")]
    [InlineData("/categorias-producto", "delete-categoriaproducto")]
    [InlineData("/unidades-medida", "delete-unidadmedida")]
    [InlineData("/terminos-pago", "delete-terminopago")]
    [InlineData("/cuentas-contables", "delete-cuenta-contable")]
    [InlineData("/grupos-contables?tipo=producto", "delete-grupo-contable")]
    [InlineData("/grupos-cliente-contable", "delete-grupo-cliente-contable")]
    [InlineData("/setups-contables?tipo=iva", "delete-setup-contable")]
    [InlineData("/clientes?deleteId={0}", "delete-cliente-list")]
    [InlineData("/productos?deleteId={0}", "delete-producto-list")]
    [InlineData("/clientes/{0}?delete=true", "cliente-delete")]
    [InlineData("/productos/{0}?delete=true", "producto-delete")]
    public async Task BorradoForzado_SinCanDelete_DevuelveMensajeDeLaApi(string ruta, string formName)
    {
        using var app = Configurar(new BlazorSsrFactory());
        var url = string.Format(System.Globalization.CultureInfo.InvariantCulture, ruta, Id);
        // Supervisor: CanModify + CanConsult, sin CanDelete.
        var supervisor = app.Cliente("Supervisor");

        var html = await HtmlAsync(supervisor, url);
        var forzado = await FormulariosSsr.EnviarAsync(supervisor, url, formName,
            new Dictionary<string, string> { ["DeleteInput.Id"] = Id.ToString() });
        var respuesta = Decodificar(await forzado.Content.ReadAsStringAsync());

        Assert.Contains($"value=\"{formName}\"", html);
        Assert.DoesNotContain("Sí, eliminar", html);
        Assert.Equal(HttpStatusCode.OK, forzado.StatusCode);
        Assert.Contains(MensajeApi, respuesta);
    }

    private static BlazorSsrFactory Configurar(BlazorSsrFactory app)
    {
        app.Simular<IAlmacenApiClient>().Setup(c => c.DeleteAsync(Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlmacenOperationResult(false, MensajeApi));
        app.Simular<ICategoriaProductoApiClient>().Setup(c => c.DeleteAsync(Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CategoriaProductoOperationResult(false, MensajeApi));
        app.Simular<ICategoriaProductoApiClient>().Setup(c => c.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        app.Simular<IUnidadMedidaApiClient>().Setup(c => c.DeleteAsync(Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UnidadMedidaOperationResult(false, MensajeApi));
        app.Simular<IUnidadMedidaApiClient>().Setup(c => c.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        app.Simular<ITerminoPagoApiClient>().Setup(c => c.DeleteAsync(Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TerminoPagoOperationResult(false, MensajeApi));
        app.Simular<ITerminoPagoApiClient>().Setup(c => c.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        app.Simular<ICuentaContableApiClient>().Setup(c => c.DeleteAsync(Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CuentaContableOperationResult(false, MensajeApi));
        app.Simular<IGrupoContableApiClient>().Setup(c => c.DeleteAsync(TipoGrupoContable.Producto, Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GrupoOperationResult(false, MensajeApi));
        app.Simular<IGrupoClienteContableApiClient>().Setup(c => c.DeleteAsync(Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GrupoOperationResult(false, MensajeApi));
        app.Simular<ISetupContableApiClient>().Setup(c => c.DeleteAsync(TipoSetupContable.Iva, Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GrupoOperationResult(false, MensajeApi));

        var cliente = new SocioNegocioResponse { Id = Id, Codigo = "C0001", NombreComercial = "Comercial Uno", Email = "uno@test.local" };
        var socios = app.Simular<ISocioNegocioApiClient>();
        socios.Setup(c => c.ListAsync(It.IsAny<SocioNegocioSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<SocioNegocioResponse>([cliente], 1, 50, 1));
        socios.Setup(c => c.ListAllAsync(It.IsAny<SocioNegocioSearchFilter?>(), It.IsAny<CancellationToken>())).ReturnsAsync([cliente]);
        socios.Setup(c => c.GetByIdAsync(Id, It.IsAny<CancellationToken>())).ReturnsAsync(cliente);
        socios.Setup(c => c.DeleteAsync(Id, It.IsAny<CancellationToken>())).ReturnsAsync(new SocioNegocioOperationResult(false, MensajeApi));

        var producto = new ProductoResponse { Id = Id, Codigo = "P0001", Nombre = "Tornillo", CategoriaCodigo = "GENERAL", CategoriaNombre = "General" };
        var productos = app.Simular<IProductoApiClient>();
        productos.Setup(c => c.ListAsync(It.IsAny<ProductoSearchFilter?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ProductoResponse>([producto], 1, 50, 1));
        productos.Setup(c => c.ListAllAsync(It.IsAny<ProductoSearchFilter?>(), It.IsAny<CancellationToken>())).ReturnsAsync([producto]);
        productos.Setup(c => c.GetByIdAsync(Id, It.IsAny<CancellationToken>())).ReturnsAsync(producto);
        productos.Setup(c => c.DeleteAsync(Id, It.IsAny<CancellationToken>())).ReturnsAsync(new ProductoOperationResult(false, MensajeApi));

        app.Simular<ICobroApiClient>();
        return app;
    }
}
