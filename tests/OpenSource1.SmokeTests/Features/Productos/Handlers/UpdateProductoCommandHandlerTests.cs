using Moq;
using OpenSource1.Application.Features.Productos.Commands;
using OpenSource1.Application.Features.Productos.Handlers;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Enums;

namespace OpenSource1.SmokeTests.Features.Productos.Handlers;

public class UpdateProductoCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFalloNoEncontrado_WhenProductoDoesNotExist()
    {
        var fake = new ProductosFake();

        var result = await new UpdateProductoCommandHandler(fake.UnitOfWork.Object)
            .Handle(new UpdateProductoCommand(Guid.NewGuid(), "C", "N", 1, 1, null, null, null, null, null), default);

        Assert.True(result.EsFallo);
        Assert.Equal("producto.no_encontrado", result.Errores[0].Codigo);
    }

    [Fact]
    public async Task Handle_UpdatesAndSaves_WhenProductoExists()
    {
        var fake = new ProductosFake();
        var entity = ProductoExistente(fake);
        var categoria = fake.AgregarCategoria("ELEC", "Electrónica");
        var unidad = fake.AgregarUnidad("KG", "Kilogramo");

        var result = await new UpdateProductoCommandHandler(fake.UnitOfWork.Object).Handle(
            new UpdateProductoCommand(entity.Id, " NEW ", " Nuevo ", 3.25m, 9, categoria.Id, unidad.Id, MetodoCosteo.Promedio, 2m, BloqueoProducto.Todo), default);

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Mensaje : null);
        Assert.Equal("NEW", entity.Codigo);
        Assert.Equal("Nuevo", entity.Nombre);
        Assert.Equal(3.25m, entity.PrecioVenta);
        Assert.Equal(9, entity.Stock);
        Assert.Equal(categoria.Id, entity.CategoriaId);
        Assert.Equal(unidad.Id, entity.UnidadMedidaBaseId);
        Assert.Equal(2m, entity.CostoEstandar);
        Assert.Equal(BloqueoProducto.Todo, entity.Bloqueado);
        Assert.Equal("ELEC", result.Valor.CategoriaCodigo);
        Assert.Equal("KG", result.Valor.UnidadMedidaCodigo);
        fake.Productos.Mock.Verify(r => r.Update(entity), Times.Once);
        fake.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SoloElNombre_ConservaTodoLoDemas()
    {
        // Lección de SocioNegocio: un PUT parcial con valores por defecto desbloqueaba a un cliente bloqueado. Aquí null = conservar.
        var fake = new ProductosFake();
        var categoria = fake.AgregarCategoria("ELEC", "Electrónica");
        var unidad = fake.AgregarUnidad("KG", "Kilogramo");
        var entity = ProductoExistente(fake);
        entity.CategoriaId = categoria.Id;
        entity.UnidadMedidaBaseId = unidad.Id;
        entity.PrecioVenta = 99.9999m;
        entity.Stock = 42;
        entity.CostoEstandar = 12.5m;
        entity.CostoUnitario = 8.75m;
        entity.CostoAjustado = false;
        entity.Bloqueado = BloqueoProducto.Venta;
        entity.MetodoCosteo = MetodoCosteo.Promedio;

        var result = await new UpdateProductoCommandHandler(fake.UnitOfWork.Object)
            .Handle(new UpdateProductoCommand(entity.Id, "OLD", "Renombrado", null, null, null, null, null, null, null), default);

        Assert.True(result.EsExito);
        Assert.Equal("Renombrado", entity.Nombre);
        Assert.Equal(99.9999m, entity.PrecioVenta);
        Assert.Equal(42, entity.Stock);
        Assert.Equal(categoria.Id, entity.CategoriaId);
        Assert.Equal(unidad.Id, entity.UnidadMedidaBaseId);
        Assert.Equal(12.5m, entity.CostoEstandar);
        Assert.Equal(BloqueoProducto.Venta, entity.Bloqueado);
        // Mantenidos por el sistema: el PUT nunca los toca.
        Assert.Equal(8.75m, entity.CostoUnitario);
        Assert.False(entity.CostoAjustado);
    }

    [Fact]
    public async Task Handle_CategoriaNuevaInexistente_DevuelveFalloYNoModifica()
    {
        var fake = new ProductosFake();
        var entity = ProductoExistente(fake);

        var result = await new UpdateProductoCommandHandler(fake.UnitOfWork.Object)
            .Handle(new UpdateProductoCommand(entity.Id, "OLD", "Otro", null, null, Guid.NewGuid(), null, null, null, null), default);

        Assert.True(result.EsFallo);
        Assert.Equal("CategoriaId", result.Errores[0].Campo);
        Assert.DoesNotContain(result.Errores, e => e.Codigo.EndsWith(".no_encontrado", StringComparison.Ordinal));
        Assert.Equal("Old", entity.Nombre);
        fake.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_GuidVacioComoCategoria_SeRechazaEnVezDeConservar()
    {
        var fake = new ProductosFake();
        var entity = ProductoExistente(fake);

        var result = await new UpdateProductoCommandHandler(fake.UnitOfWork.Object)
            .Handle(new UpdateProductoCommand(entity.Id, "OLD", "Otro", null, null, Guid.Empty, null, null, null, null), default);

        Assert.True(result.EsFallo);
        Assert.Equal("CategoriaId", result.Errores[0].Campo);
    }

    [Fact]
    public async Task Handle_UnidadNuevaBorrada_SeRechaza()
    {
        var fake = new ProductosFake();
        var entity = ProductoExistente(fake);
        var borrada = fake.AgregarUnidad("VIEJA", "Vieja");
        borrada.IsDeleted = true;

        var result = await new UpdateProductoCommandHandler(fake.UnitOfWork.Object)
            .Handle(new UpdateProductoCommand(entity.Id, "OLD", "Otro", null, null, null, borrada.Id, null, null, null), default);

        Assert.True(result.EsFallo);
        Assert.Equal("UnidadMedidaBaseId", result.Errores[0].Campo);
    }

    [Fact]
    public async Task Handle_CategoriaGuardadaYaBorrada_NoSeRevalidaSiNoCambia()
    {
        // Un producto con una referencia ya colgante (dato previo a los guardas de borrado) debe seguir siendo editable.
        var fake = new ProductosFake();
        var vieja = fake.AgregarCategoria("VIEJA", "Vieja");
        var entity = ProductoExistente(fake);
        entity.CategoriaId = vieja.Id;
        vieja.IsDeleted = true;

        var result = await new UpdateProductoCommandHandler(fake.UnitOfWork.Object)
            .Handle(new UpdateProductoCommand(entity.Id, "OLD", "Renombrado", null, null, null, null, null, null, null), default);

        Assert.True(result.EsExito);
        Assert.Equal(vieja.Id, entity.CategoriaId);
        Assert.Equal("Renombrado", entity.Nombre);
    }

    [Theory]
    [InlineData(-1, 0, 0, "PrecioVenta")]
    [InlineData(1.00001, 0, 0, "PrecioVenta")]
    [InlineData(1, -1, 0, "Stock")]
    [InlineData(1, 0, -1, "CostoEstandar")]
    [InlineData(1, 0, 9, "Bloqueado")]
    public async Task Handle_ValoresInformadosInvalidos_DevuelveFalloConElCampo(double precio, int stock, double costoOBloqueo, string campo)
    {
        var fake = new ProductosFake();
        var entity = ProductoExistente(fake);

        var comando = campo switch
        {
            "Bloqueado" => new UpdateProductoCommand(entity.Id, "OLD", "N", null, null, null, null, null, null, (BloqueoProducto)(int)costoOBloqueo),
            "CostoEstandar" => new UpdateProductoCommand(entity.Id, "OLD", "N", null, null, null, null, null, (decimal)costoOBloqueo, null),
            "Stock" => new UpdateProductoCommand(entity.Id, "OLD", "N", null, stock, null, null, null, null, null),
            _ => new UpdateProductoCommand(entity.Id, "OLD", "N", (decimal)precio, null, null, null, null, null, null),
        };

        var result = await new UpdateProductoCommandHandler(fake.UnitOfWork.Object).Handle(comando, default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Campo == campo);
        fake.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MetodoDeCosteoNoDefinido_DevuelveFallo()
    {
        var fake = new ProductosFake();
        var entity = ProductoExistente(fake);

        var result = await new UpdateProductoCommandHandler(fake.UnitOfWork.Object)
            .Handle(new UpdateProductoCommand(entity.Id, "OLD", "N", null, null, null, null, (MetodoCosteo)5, null, null), default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Campo == "MetodoCosteo");
    }

    private static Producto ProductoExistente(ProductosFake fake) => fake.Productos.Agregar(new Producto
    {
        Codigo = "OLD",
        Nombre = "Old",
        PrecioVenta = 1,
        Stock = 1,
        CategoriaId = fake.General.Id,
        UnidadMedidaBaseId = fake.Unidad.Id,
    });
}
