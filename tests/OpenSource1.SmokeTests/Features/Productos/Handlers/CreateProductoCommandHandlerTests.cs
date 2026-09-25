using Moq;
using OpenSource1.Application.Features.Productos.Commands;
using OpenSource1.Application.Features.Productos.Handlers;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Enums;

namespace OpenSource1.SmokeTests.Features.Productos.Handlers;

public class CreateProductoCommandHandlerTests
{
    [Fact]
    public async Task Handle_AddsEntity_SavesAndReturnsMappedResponse()
    {
        var fake = new ProductosFake();
        var categoria = fake.AgregarCategoria("ELEC", "Electrónica");
        var unidad = fake.AgregarUnidad("KG", "Kilogramo");

        var handler = new CreateProductoCommandHandler(fake.UnitOfWork.Object);
        var result = await handler.Handle(
            new CreateProductoCommand("  COD-1 ", "  Producto ", 10.5m, 5, categoria.Id, unidad.Id, MetodoCosteo.Promedio, 7.25m, BloqueoProducto.Venta), default);

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Mensaje : null);
        var added = Assert.Single(fake.Productos.Datos);
        Assert.Equal("COD-1", added.Codigo);
        Assert.Equal("Producto", added.Nombre);
        Assert.Equal(10.5m, added.PrecioVenta);
        Assert.Equal(5, added.Stock);
        Assert.Equal(categoria.Id, added.CategoriaId);
        Assert.Equal(unidad.Id, added.UnidadMedidaBaseId);
        Assert.Equal(7.25m, added.CostoEstandar);
        Assert.Equal(BloqueoProducto.Venta, added.Bloqueado);
        // Los mantiene el sistema: un producto nuevo no tiene movimientos que ajustar.
        Assert.Equal(0m, added.CostoUnitario);
        Assert.True(added.CostoAjustado);

        Assert.Equal(added.Id, result.Valor.Id);
        Assert.Equal("ELEC", result.Valor.CategoriaCodigo);
        Assert.Equal("Electrónica", result.Valor.CategoriaNombre);
        Assert.Equal("KG", result.Valor.UnidadMedidaCodigo);
        Assert.Equal("Kilogramo", result.Valor.UnidadMedidaNombre);
        Assert.Equal(10.5m, result.Valor.PrecioVenta);
        fake.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SinCategoriaNiUnidad_UsaGeneralYUnd()
    {
        var fake = new ProductosFake();
        fake.AgregarCategoria("OTRA", "Otra");

        var result = await new CreateProductoCommandHandler(fake.UnitOfWork.Object)
            .Handle(new CreateProductoCommand("C", "N", 1m, 0), default);

        Assert.True(result.EsExito);
        var added = Assert.Single(fake.Productos.Datos);
        Assert.Equal(fake.General.Id, added.CategoriaId);
        Assert.Equal(fake.Unidad.Id, added.UnidadMedidaBaseId);
        Assert.Equal(MetodoCosteo.Promedio, added.MetodoCosteo);
        Assert.Equal(BloqueoProducto.Ninguno, added.Bloqueado);
    }

    [Fact]
    public async Task Handle_CategoriaInexistente_DevuelveFalloConCampoCategoriaIdQueNoEsNoEncontrado()
    {
        var fake = new ProductosFake();

        var result = await new CreateProductoCommandHandler(fake.UnitOfWork.Object)
            .Handle(new CreateProductoCommand("C", "N", 1m, 0, Guid.NewGuid()), default);

        Assert.True(result.EsFallo);
        Assert.Equal("CategoriaId", result.Errores[0].Campo);
        // Un código ".no_encontrado" se traduciría a 404 (recurso de la URL): es un dato inválido del cuerpo (400).
        Assert.DoesNotContain(result.Errores, e => e.Codigo.EndsWith(".no_encontrado", StringComparison.Ordinal));
        Assert.Empty(fake.Productos.Datos);
    }

    [Fact]
    public async Task Handle_CategoriaBorradaLogicamente_SeRechaza()
    {
        var fake = new ProductosFake();
        var borrada = fake.AgregarCategoria("VIEJA", "Vieja");
        borrada.IsDeleted = true;

        var result = await new CreateProductoCommandHandler(fake.UnitOfWork.Object)
            .Handle(new CreateProductoCommand("C", "N", 1m, 0, borrada.Id), default);

        Assert.True(result.EsFallo);
        Assert.Equal("CategoriaId", result.Errores[0].Campo);
    }

    [Fact]
    public async Task Handle_UnidadInexistente_DevuelveFalloConCampoUnidadMedidaBaseId()
    {
        var fake = new ProductosFake();

        var result = await new CreateProductoCommandHandler(fake.UnitOfWork.Object)
            .Handle(new CreateProductoCommand("C", "N", 1m, 0, null, Guid.NewGuid()), default);

        Assert.True(result.EsFallo);
        Assert.Equal("UnidadMedidaBaseId", result.Errores[0].Campo);
        Assert.DoesNotContain(result.Errores, e => e.Codigo.EndsWith(".no_encontrado", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Handle_SinLaCategoriaPorDefectoYSinCategoria_DevuelveFallo()
    {
        var fake = new ProductosFake();
        fake.General.IsDeleted = true;

        var result = await new CreateProductoCommandHandler(fake.UnitOfWork.Object)
            .Handle(new CreateProductoCommand("C", "N", 1m, 0), default);

        Assert.True(result.EsFallo);
        Assert.Equal("CategoriaId", result.Errores[0].Campo);
    }

    [Theory]
    [InlineData("", "N", 1, 0, 0, 0, "Codigo")]
    [InlineData("C", " ", 1, 0, 0, 0, "Nombre")]
    [InlineData("C", "N", -1, 0, 0, 0, "PrecioVenta")]
    [InlineData("C", "N", 1.00001, 0, 0, 0, "PrecioVenta")]
    [InlineData("C", "N", 1, -1, 0, 0, "Stock")]
    [InlineData("C", "N", 1, 0, -0.5, 0, "CostoEstandar")]
    [InlineData("C", "N", 1, 0, 1.23456, 0, "CostoEstandar")]
    [InlineData("C", "N", 1, 0, 0, 9, "Bloqueado")]
    public async Task Handle_DatosInvalidos_DevuelveFalloConElCampoDelDto(
        string codigo, string nombre, double precio, int stock, double costo, int bloqueo, string campo)
    {
        var fake = new ProductosFake();

        var result = await new CreateProductoCommandHandler(fake.UnitOfWork.Object).Handle(
            new CreateProductoCommand(codigo, nombre, (decimal)precio, stock, null, null, MetodoCosteo.Promedio, (decimal)costo, (BloqueoProducto)bloqueo), default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Campo == campo);
        Assert.Empty(fake.Productos.Datos);
    }

    [Fact]
    public async Task Handle_MetodoDeCosteoNoDefinido_DevuelveFallo()
    {
        var fake = new ProductosFake();

        var result = await new CreateProductoCommandHandler(fake.UnitOfWork.Object).Handle(
            new CreateProductoCommand("C", "N", 1m, 0, null, null, (MetodoCosteo)7), default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Campo == "MetodoCosteo");
    }

    [Fact]
    public async Task Handle_RutaDeImagenInvalida_DevuelveFalloConCampoImagePath()
    {
        var fake = new ProductosFake();

        var result = await new CreateProductoCommandHandler(fake.UnitOfWork.Object).Handle(
            new CreateProductoCommand("C", "N", 1m, 0, ImagePath: "/uploads/clientes/x.png"), default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Campo == "ImagePath");
    }

    [Fact]
    public async Task Handle_ImagenYaAsignadaAOtroProducto_DevuelveFallo()
    {
        var fake = new ProductosFake();
        fake.Productos.Agregar(new Producto
        {
            Codigo = "OTRO", Nombre = "Otro", CategoriaId = fake.General.Id, UnidadMedidaBaseId = fake.Unidad.Id, ImagePath = "/uploads/productos/a.png"
        });

        var result = await new CreateProductoCommandHandler(fake.UnitOfWork.Object).Handle(
            new CreateProductoCommand("C", "N", 1m, 0, ImagePath: "/uploads/productos/a.png"), default);

        Assert.True(result.EsFallo);
        Assert.Equal("producto.imagen_en_uso", result.Errores[0].Codigo);
        Assert.Single(fake.Productos.Datos);
    }
}
