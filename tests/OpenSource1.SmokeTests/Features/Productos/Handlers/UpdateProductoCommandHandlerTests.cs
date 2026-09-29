using Moq;
using OpenSource1.Application.Features.Productos.Commands;
using OpenSource1.Application.Features.Productos.Handlers;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Enums;

namespace OpenSource1.SmokeTests.Features.Productos.Handlers;

public class UpdateProductoCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFalloNoEncontrado_WhenProductoDoesNotExist()
    {
        var fake = new ProductosFake();

        var result = await Handler(fake)
            .Handle(new UpdateProductoCommand(Guid.NewGuid(), "C", "N", 1, null, null, null, null, null), default);

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

        var result = await Handler(fake, existencia: 4.5m).Handle(
            new UpdateProductoCommand(entity.Id, " NEW ", " Nuevo ", 3.25m, categoria.Id, unidad.Id, MetodoCosteo.Promedio, 2m, BloqueoProducto.Todo), default);

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Mensaje : null);
        Assert.Equal("NEW", entity.Codigo);
        Assert.Equal("Nuevo", entity.Nombre);
        Assert.Equal(3.25m, entity.PrecioVenta);
        Assert.Equal(categoria.Id, entity.CategoriaId);
        Assert.Equal(unidad.Id, entity.UnidadMedidaBaseId);
        Assert.Equal(2m, entity.CostoEstandar);
        Assert.Equal(BloqueoProducto.Todo, entity.Bloqueado);
        Assert.Equal("ELEC", result.Valor.CategoriaCodigo);
        Assert.Equal("KG", result.Valor.UnidadMedidaCodigo);
        // La existencia de la respuesta viene del libro (Task 3.6), no del comando.
        Assert.Equal(4.5m, result.Valor.Existencia);
        fake.Productos.Mock.Verify(r => r.Update(entity), Times.Once);
        fake.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
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
        entity.CostoEstandar = 12.5m;
        entity.CostoUnitario = 8.75m;
        entity.CostoAjustado = false;
        entity.Bloqueado = BloqueoProducto.Venta;
        entity.MetodoCosteo = MetodoCosteo.Promedio;

        var result = await Handler(fake)
            .Handle(new UpdateProductoCommand(entity.Id, "OLD", "Renombrado", null, null, null, null, null, null), default);

        Assert.True(result.EsExito);
        Assert.Equal("Renombrado", entity.Nombre);
        Assert.Equal(99.9999m, entity.PrecioVenta);
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

        var result = await Handler(fake)
            .Handle(new UpdateProductoCommand(entity.Id, "OLD", "Otro", null, Guid.NewGuid(), null, null, null, null), default);

        Assert.True(result.EsFallo);
        Assert.Equal("CategoriaId", result.Errores[0].Campo);
        Assert.DoesNotContain(result.Errores, e => e.Codigo.EndsWith(".no_encontrado", StringComparison.Ordinal));
        Assert.Equal("Old", entity.Nombre);
        fake.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_GuidVacioComoCategoria_SeRechazaEnVezDeConservar()
    {
        var fake = new ProductosFake();
        var entity = ProductoExistente(fake);

        var result = await Handler(fake)
            .Handle(new UpdateProductoCommand(entity.Id, "OLD", "Otro", null, Guid.Empty, null, null, null, null), default);

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

        var result = await Handler(fake)
            .Handle(new UpdateProductoCommand(entity.Id, "OLD", "Otro", null, null, borrada.Id, null, null, null), default);

        Assert.True(result.EsFallo);
        Assert.Equal("UnidadMedidaBaseId", result.Errores[0].Campo);
    }

    [Fact]
    public async Task Handle_CambiaUnidadBaseConMovimientosDeInventario_DevuelveConflictoYNoModifica()
    {
        // IMPORTANT 3 (ronda 1): la unidad base es el factor con el que el libro interpreta TODO el historial ya
        // registrado; cambiarla en silencio lo reinterpretaría de golpe.
        var fake = new ProductosFake();
        var entity = ProductoExistente(fake);
        var nueva = fake.AgregarUnidad("KG", "Kilogramo");
        fake.AgregarMovimiento(entity.Id);

        var result = await Handler(fake)
            .Handle(new UpdateProductoCommand(entity.Id, "OLD", "Otro", null, null, nueva.Id, null, null, null), default);

        Assert.True(result.EsFallo);
        Assert.Equal("producto.conflicto", result.Errores[0].Codigo);
        Assert.Equal("UnidadMedidaBaseId", result.Errores[0].Campo);
        Assert.Equal(fake.Unidad.Id, entity.UnidadMedidaBaseId);
        fake.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CambiaUnidadBaseSinMovimientos_Permite()
    {
        var fake = new ProductosFake();
        var entity = ProductoExistente(fake);
        var nueva = fake.AgregarUnidad("KG", "Kilogramo");

        var result = await Handler(fake)
            .Handle(new UpdateProductoCommand(entity.Id, "OLD", "Otro", null, null, nueva.Id, null, null, null), default);

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Mensaje : null);
        Assert.Equal(nueva.Id, entity.UnidadMedidaBaseId);
    }

    [Fact]
    public async Task Handle_MismaUnidadBaseConMovimientos_NoSeRevisaLaGuarda()
    {
        // La unidad "cambia" a sí misma (mismo Id, con o sin movimientos): no hay reinterpretación real, se permite.
        var fake = new ProductosFake();
        var entity = ProductoExistente(fake);
        fake.AgregarMovimiento(entity.Id);

        var result = await Handler(fake)
            .Handle(new UpdateProductoCommand(entity.Id, "OLD", "Otro", null, null, fake.Unidad.Id, null, null, null), default);

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Mensaje : null);
        Assert.Equal(fake.Unidad.Id, entity.UnidadMedidaBaseId);
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

        var result = await Handler(fake)
            .Handle(new UpdateProductoCommand(entity.Id, "OLD", "Renombrado", null, null, null, null, null, null), default);

        Assert.True(result.EsExito);
        Assert.Equal(vieja.Id, entity.CategoriaId);
        Assert.Equal("Renombrado", entity.Nombre);
    }

    [Theory]
    [InlineData(-1, 0, "PrecioVenta")]
    [InlineData(1.00001, 0, "PrecioVenta")]
    [InlineData(1, -1, "CostoEstandar")]
    [InlineData(1, 9, "Bloqueado")]
    public async Task Handle_ValoresInformadosInvalidos_DevuelveFalloConElCampo(double precio, double costoOBloqueo, string campo)
    {
        var fake = new ProductosFake();
        var entity = ProductoExistente(fake);

        var comando = campo switch
        {
            "Bloqueado" => new UpdateProductoCommand(entity.Id, "OLD", "N", null, null, null, null, null, (BloqueoProducto)(int)costoOBloqueo),
            "CostoEstandar" => new UpdateProductoCommand(entity.Id, "OLD", "N", null, null, null, null, (decimal)costoOBloqueo, null),
            _ => new UpdateProductoCommand(entity.Id, "OLD", "N", (decimal)precio, null, null, null, null, null),
        };

        var result = await Handler(fake).Handle(comando, default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Campo == campo);
        fake.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MetodoDeCosteoNoDefinido_DevuelveFallo()
    {
        var fake = new ProductosFake();
        var entity = ProductoExistente(fake);

        var result = await Handler(fake)
            .Handle(new UpdateProductoCommand(entity.Id, "OLD", "N", null, null, null, (MetodoCosteo)5, null, null), default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Campo == "MetodoCosteo");
    }

    private static Producto ProductoExistente(ProductosFake fake) => fake.Productos.Agregar(new Producto
    {
        Codigo = "OLD",
        Nombre = "Old",
        PrecioVenta = 1,
        CategoriaId = fake.General.Id,
        UnidadMedidaBaseId = fake.Unidad.Id,
    });

    /// <summary>El handler consulta el libro (Task 3.6) solo para poblar <c>Existencia</c> en la respuesta; el mock siempre
    /// puede responder porque un <see cref="OpenSource1.Application.Features.Productos.Commands.UpdateProductoCommand"/> con
    /// fallo nunca llega a invocarlo.</summary>
    private static UpdateProductoCommandHandler Handler(ProductosFake fake, decimal existencia = 0m)
    {
        var consultaInventario = new Mock<IConsultaInventario>();
        consultaInventario
            .Setup(c => c.ExistenciaAsync(It.IsAny<Guid>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existencia);
        return new UpdateProductoCommandHandler(fake.UnitOfWork.Object, consultaInventario.Object, new Mock<IRegistroMovimientosInventario>().Object);
    }
}
