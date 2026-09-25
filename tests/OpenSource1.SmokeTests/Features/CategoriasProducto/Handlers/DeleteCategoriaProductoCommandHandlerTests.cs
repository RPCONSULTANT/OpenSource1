using Moq;
using OpenSource1.Application.Features.CategoriasProducto.Commands;
using OpenSource1.Application.Features.CategoriasProducto.Handlers;
using OpenSource1.Core.Entities;

namespace OpenSource1.SmokeTests.Features.CategoriasProducto.Handlers;

public class DeleteCategoriaProductoCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFallo_WhenCategoriaDoesNotExist()
    {
        var fake = new CategoriaProductoRepoFake();
        var handler = new DeleteCategoriaProductoCommandHandler(fake.UnitOfWork.Object);

        var result = await handler.Handle(new DeleteCategoriaProductoCommand(Guid.NewGuid()), default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Codigo == "categoria_producto.no_encontrado");
    }

    [Fact]
    public async Task Handle_RemovesAndSaves_WhenCategoriaNoTieneHijos()
    {
        var fake = new CategoriaProductoRepoFake();
        var padre = fake.Agregar("ALIM");
        var hoja = fake.Agregar("LACT", padre.Id);
        var handler = new DeleteCategoriaProductoCommandHandler(fake.UnitOfWork.Object);

        var result = await handler.Handle(new DeleteCategoriaProductoCommand(hoja.Id), default);

        Assert.True(result.EsExito);
        fake.Repo.Verify(r => r.Remove(hoja), Times.Once);
        fake.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsConflicto_WhenCategoriaTieneHijos()
    {
        var fake = new CategoriaProductoRepoFake();
        var padre = fake.Agregar("ALIM");
        fake.Agregar("LACT", padre.Id);
        var handler = new DeleteCategoriaProductoCommandHandler(fake.UnitOfWork.Object);

        var result = await handler.Handle(new DeleteCategoriaProductoCommand(padre.Id), default);

        Assert.True(result.EsFallo);
        Assert.Equal("categoria_producto.con_hijos.conflicto", result.Errores[0].Codigo);
        fake.Repo.Verify(r => r.Remove(It.IsAny<CategoriaProducto>()), Times.Never);
        fake.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RemovesAndSaves_WhenLosUnicosHijosYaEstanBorradosLogicamente()
    {
        var fake = new CategoriaProductoRepoFake();
        var padre = fake.Agregar("ALIM");
        var hijo = fake.Agregar("LACT", padre.Id);
        fake.Borrar(hijo);
        var handler = new DeleteCategoriaProductoCommandHandler(fake.UnitOfWork.Object);

        var result = await handler.Handle(new DeleteCategoriaProductoCommand(padre.Id), default);

        Assert.True(result.EsExito);
        fake.Repo.Verify(r => r.Remove(padre), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsConflicto_WhenLaCategoriaTieneProductos()
    {
        var fake = new CategoriaProductoRepoFake();
        var categoria = fake.Agregar("ALIM");
        fake.Productos.Agregar(new Producto { Codigo = "P1", Nombre = "P", CategoriaId = categoria.Id, UnidadMedidaBaseId = Guid.NewGuid() });
        var handler = new DeleteCategoriaProductoCommandHandler(fake.UnitOfWork.Object);

        var result = await handler.Handle(new DeleteCategoriaProductoCommand(categoria.Id), default);

        Assert.True(result.EsFallo);
        Assert.Equal("categoria_producto.en_uso.conflicto", result.Errores[0].Codigo);
        fake.Repo.Verify(r => r.Remove(It.IsAny<CategoriaProducto>()), Times.Never);
        fake.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RemovesAndSaves_WhenLosUnicosProductosYaEstanBorradosLogicamente()
    {
        var fake = new CategoriaProductoRepoFake();
        var categoria = fake.Agregar("ALIM");
        fake.Productos.Agregar(new Producto { Codigo = "P1", Nombre = "P", CategoriaId = categoria.Id, UnidadMedidaBaseId = Guid.NewGuid(), IsDeleted = true });
        var handler = new DeleteCategoriaProductoCommandHandler(fake.UnitOfWork.Object);

        var result = await handler.Handle(new DeleteCategoriaProductoCommand(categoria.Id), default);

        Assert.True(result.EsExito);
        fake.Repo.Verify(r => r.Remove(categoria), Times.Once);
    }
}
