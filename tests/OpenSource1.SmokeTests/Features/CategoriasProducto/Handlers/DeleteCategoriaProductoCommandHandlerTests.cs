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
}
