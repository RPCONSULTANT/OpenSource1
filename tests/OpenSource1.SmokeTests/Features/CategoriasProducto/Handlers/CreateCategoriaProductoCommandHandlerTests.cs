using Moq;
using OpenSource1.Application.Features.CategoriasProducto.Commands;
using OpenSource1.Application.Features.CategoriasProducto.Handlers;
using OpenSource1.Core.Entities;

namespace OpenSource1.SmokeTests.Features.CategoriasProducto.Handlers;

public class CreateCategoriaProductoCommandHandlerTests
{
    [Fact]
    public async Task Handle_AddsEntity_SavesAndReturnsMappedResponse()
    {
        var fake = new CategoriaProductoRepoFake();
        var handler = new CreateCategoriaProductoCommandHandler(fake.UnitOfWork.Object);

        var result = await handler.Handle(new CreateCategoriaProductoCommand("  bebidas  ", "  Bebidas  ", null), default);

        Assert.True(result.EsExito);
        Assert.Equal("BEBIDAS", result.Valor.Codigo);
        Assert.Equal("Bebidas", result.Valor.Nombre);
        Assert.Null(result.Valor.CategoriaPadreId);
        Assert.Null(result.Valor.CategoriaPadreNombre);
        fake.Repo.Verify(r => r.AddAsync(It.Is<CategoriaProducto>(c => c.Codigo == "BEBIDAS"), It.IsAny<CancellationToken>()), Times.Once);
        fake.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ConPadreExistente_AsignaElPadreYDevuelveSuNombre()
    {
        var fake = new CategoriaProductoRepoFake();
        var padre = fake.Agregar("ALIM");
        var handler = new CreateCategoriaProductoCommandHandler(fake.UnitOfWork.Object);

        var result = await handler.Handle(new CreateCategoriaProductoCommand("LACT", "Lácteos", padre.Id), default);

        Assert.True(result.EsExito);
        Assert.Equal(padre.Id, result.Valor.CategoriaPadreId);
        Assert.Equal(padre.Nombre, result.Valor.CategoriaPadreNombre);
    }

    [Fact]
    public async Task Handle_ReturnsFallo_WhenPadreNoExiste()
    {
        var fake = new CategoriaProductoRepoFake();
        var handler = new CreateCategoriaProductoCommandHandler(fake.UnitOfWork.Object);

        var result = await handler.Handle(new CreateCategoriaProductoCommand("LACT", "Lácteos", Guid.NewGuid()), default);

        Assert.True(result.EsFallo);
        var error = Assert.Single(result.Errores);
        Assert.Equal("categoria_producto.padre_no_encontrado", error.Codigo);
        Assert.Equal("CategoriaPadreId", error.Campo);
        fake.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsFallo_WhenPadreEstaBorradoLogicamente()
    {
        var fake = new CategoriaProductoRepoFake();
        var padre = fake.Agregar("ALIM");
        fake.Borrar(padre);
        var handler = new CreateCategoriaProductoCommandHandler(fake.UnitOfWork.Object);

        var result = await handler.Handle(new CreateCategoriaProductoCommand("LACT", "Lácteos", padre.Id), default);

        Assert.Equal("categoria_producto.padre_no_encontrado", Assert.Single(result.Errores).Codigo);
    }

    [Theory]
    [InlineData("", "Nombre")]
    [InlineData("CODIGO-DE-CATEGORIA-MUY-LARGO-XXXX", "Nombre")]
    [InlineData("COD", "")]
    [InlineData("COD", "N")]
    public async Task Handle_ReturnsFallo_WhenDatosInvalidos(string codigo, string nombre)
    {
        if (nombre == "N")
        {
            nombre = new string('n', 101);
        }

        var fake = new CategoriaProductoRepoFake();
        var handler = new CreateCategoriaProductoCommandHandler(fake.UnitOfWork.Object);

        var result = await handler.Handle(new CreateCategoriaProductoCommand(codigo, nombre, null), default);

        Assert.True(result.EsFallo);
        fake.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
