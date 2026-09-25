extern alias BlazorApp;
using System.ComponentModel.DataAnnotations;
using BlazorApp::OpenSource1.Blazor.Components;
using BlazorApp::OpenSource1.Blazor.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenSource1.Application.Features.CategoriasProducto.Dtos;
using OpenSource1.Application.Features.UnidadesMedida.Dtos;

namespace OpenSource1.SmokeTests.Blazor;

public sealed class ProductoFormularioTests
{
    private static List<ValidationResult> Validar(ProductoEditorForm form)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(form, new ValidationContext(form), resultados, validateAllProperties: true);
        return resultados;
    }

    [Fact]
    public void Formulario_ConImportesValidos_NoTieneErrores_YExponeLosDecimalesInterpretados()
    {
        var form = new ProductoEditorForm { Codigo = "C", Nombre = "N", PrecioVentaTexto = "1500,50", CostoEstandarTexto = "2.25" };

        Assert.Empty(Validar(form));
        Assert.Equal(1500.50m, form.PrecioVenta);
        Assert.Equal(2.25m, form.CostoEstandar);
    }

    [Theory]
    [InlineData("abc", "PrecioVentaTexto", "El precio de venta no es válido")]
    [InlineData("-1", "PrecioVentaTexto", "El precio de venta no puede ser negativo")]
    [InlineData("1.00001", "PrecioVentaTexto", "El precio de venta admite como máximo 4 decimales")]
    public void Formulario_ConPrecioInvalido_SeñalaElCampoConUnMensajeDelPrecio(string texto, string campo, string mensaje)
    {
        var form = new ProductoEditorForm { Codigo = "C", Nombre = "N", PrecioVentaTexto = texto };

        var error = Assert.Single(Validar(form));
        Assert.Contains(campo, error.MemberNames);
        Assert.StartsWith(mensaje, error.ErrorMessage);
    }

    [Fact]
    public void Formulario_ConCostoEstandarInvalido_SeñalaElCampoDelCosto()
    {
        var form = new ProductoEditorForm { Codigo = "C", Nombre = "N", CostoEstandarTexto = "1,500" };

        var error = Assert.Single(Validar(form));
        Assert.Contains("CostoEstandarTexto", error.MemberNames);
        Assert.StartsWith("El costo estándar es ambiguo", error.ErrorMessage);
    }

    [Fact]
    public async Task Opciones_ConCargaCorrecta_TraeCategoriasYUnidades_YNoBloqueaLaModificacion()
    {
        var categorias = new Mock<ICategoriaProductoApiClient>();
        categorias.Setup(c => c.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new CategoriaProductoResponse { Id = Guid.NewGuid(), Codigo = "A", Nombre = "A" }]);
        var unidades = new Mock<IUnidadMedidaApiClient>();
        unidades.Setup(u => u.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new UnidadMedidaResponse { Id = Guid.NewGuid(), Codigo = "UND", Nombre = "Unidad" }]);

        var opciones = await ProductoOpciones.CargarAsync(categorias.Object, unidades.Object, NullLogger.Instance);

        Assert.False(opciones.CargaFallida);
        Assert.Single(opciones.Categorias);
        Assert.Single(opciones.Unidades);
        Assert.False(opciones.ModificacionBloqueada);
    }

    [Fact]
    public async Task Opciones_SiUnaDeLasCargasFalla_MarcaCargaFallidaYBloqueaLaModificacion()
    {
        var categorias = new Mock<ICategoriaProductoApiClient>();
        categorias.Setup(c => c.ListAllAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("500"));
        var unidades = new Mock<IUnidadMedidaApiClient>();
        unidades.Setup(u => u.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new UnidadMedidaResponse { Id = Guid.NewGuid(), Codigo = "UND", Nombre = "Unidad" }]);

        var opciones = await ProductoOpciones.CargarAsync(categorias.Object, unidades.Object, NullLogger.Instance);

        Assert.True(opciones.CargaFallida);
        Assert.Empty(opciones.Categorias);
        Assert.Empty(opciones.Unidades);
        Assert.True(opciones.ModificacionBloqueada);
    }

    [Fact]
    public async Task Opciones_ConListadosVacios_BloqueanLaModificacionPeroNoElAlta()
    {
        var categorias = new Mock<ICategoriaProductoApiClient>();
        categorias.Setup(c => c.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var unidades = new Mock<IUnidadMedidaApiClient>();
        unidades.Setup(u => u.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var opciones = await ProductoOpciones.CargarAsync(categorias.Object, unidades.Object, NullLogger.Instance);

        Assert.False(opciones.CargaFallida);
        Assert.True(opciones.ModificacionBloqueada);
    }

    [Fact]
    public async Task Opciones_SiUnListadoSeCuelga_SeCortaConElTiempoMaximoYMarcaCargaFallida()
    {
        var categorias = new Mock<ICategoriaProductoApiClient>();
        categorias.Setup(c => c.ListAllAsync(It.IsAny<CancellationToken>())).Returns(async (CancellationToken ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return (IReadOnlyList<CategoriaProductoResponse>)[];
        });
        var unidades = new Mock<IUnidadMedidaApiClient>();
        unidades.Setup(u => u.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var cronometro = System.Diagnostics.Stopwatch.StartNew();
        var opciones = await ProductoOpciones.CargarAsync(categorias.Object, unidades.Object, NullLogger.Instance);
        cronometro.Stop();

        Assert.True(opciones.CargaFallida);
        Assert.True(cronometro.Elapsed < TimeSpan.FromSeconds(30), $"La carga tardó {cronometro.Elapsed}");
    }
}
