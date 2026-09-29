using Moq;
using OpenSource1.Application.Features.CategoriasProducto.Commands;
using OpenSource1.Application.Features.CategoriasProducto.Handlers;

namespace OpenSource1.SmokeTests.Features.CategoriasProducto.Handlers;

public class UpdateCategoriaProductoCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFallo_WhenCategoriaDoesNotExist()
    {
        var fake = new CategoriaProductoRepoFake();
        var handler = new UpdateCategoriaProductoCommandHandler(fake.UnitOfWork.Object);

        var result = await handler.Handle(new UpdateCategoriaProductoCommand(Guid.NewGuid(), "COD", "Nombre", null), default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Codigo == "categoria_producto.no_encontrado");
    }

    [Fact]
    public async Task Handle_UpdatesAndSaves_WhenCategoriaExists()
    {
        var fake = new CategoriaProductoRepoFake();
        var padre = fake.Agregar("ALIM");
        var entity = fake.Agregar("OLD");
        var handler = new UpdateCategoriaProductoCommandHandler(fake.UnitOfWork.Object);

        var result = await handler.Handle(new UpdateCategoriaProductoCommand(entity.Id, " new ", " Nueva ", padre.Id), default);

        Assert.True(result.EsExito);
        Assert.Equal("NEW", entity.Codigo);
        Assert.Equal("Nueva", entity.Nombre);
        Assert.Equal(padre.Id, entity.CategoriaPadreId);
        Assert.Equal(padre.Nombre, result.Valor.CategoriaPadreNombre);
        fake.Repo.Verify(r => r.Update(entity), Times.Once);
        fake.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PermiteQuitarElPadre()
    {
        var fake = new CategoriaProductoRepoFake();
        var padre = fake.Agregar("ALIM");
        var entity = fake.Agregar("LACT", padre.Id);
        var handler = new UpdateCategoriaProductoCommandHandler(fake.UnitOfWork.Object);

        var result = await handler.Handle(new UpdateCategoriaProductoCommand(entity.Id, "LACT", "Lácteos", null), default);

        Assert.True(result.EsExito);
        Assert.Null(entity.CategoriaPadreId);
    }

    [Fact]
    public async Task Handle_ReturnsFallo_WhenCategoriaEsSuPropioPadre()
    {
        var fake = new CategoriaProductoRepoFake();
        var entity = fake.Agregar("A");
        var handler = new UpdateCategoriaProductoCommandHandler(fake.UnitOfWork.Object);

        var result = await handler.Handle(new UpdateCategoriaProductoCommand(entity.Id, "A", "A", entity.Id), default);

        AssertRechazadoSinGuardar(result.Errores, "categoria_producto.padre_propio", fake);
        Assert.Null(entity.CategoriaPadreId);
    }

    [Fact]
    public async Task Handle_ReturnsFallo_WhenPadreNoExiste()
    {
        var fake = new CategoriaProductoRepoFake();
        var entity = fake.Agregar("A");
        var handler = new UpdateCategoriaProductoCommandHandler(fake.UnitOfWork.Object);

        var result = await handler.Handle(new UpdateCategoriaProductoCommand(entity.Id, "A", "A", Guid.NewGuid()), default);

        AssertRechazadoSinGuardar(result.Errores, "categoria_producto.padre_no_encontrado", fake);
    }

    [Fact]
    public async Task Handle_ReturnsFallo_WhenPadreEstaBorradoLogicamente()
    {
        var fake = new CategoriaProductoRepoFake();
        var padre = fake.Agregar("ALIM");
        var entity = fake.Agregar("LACT");
        fake.Borrar(padre);
        var handler = new UpdateCategoriaProductoCommandHandler(fake.UnitOfWork.Object);

        var result = await handler.Handle(new UpdateCategoriaProductoCommand(entity.Id, "LACT", "Lácteos", padre.Id), default);

        AssertRechazadoSinGuardar(result.Errores, "categoria_producto.padre_no_encontrado", fake);
        Assert.Null(entity.CategoriaPadreId);
    }

    [Fact]
    public async Task Handle_ReturnsFallo_WhenCicloDeDosNiveles()
    {
        // B es hija de A; hacer que A tenga por padre a B cerraría A→B→A.
        var fake = new CategoriaProductoRepoFake();
        var a = fake.Agregar("A");
        var b = fake.Agregar("B", a.Id);
        var handler = new UpdateCategoriaProductoCommandHandler(fake.UnitOfWork.Object);

        var result = await handler.Handle(new UpdateCategoriaProductoCommand(a.Id, "A", "A", b.Id), default);

        AssertRechazadoSinGuardar(result.Errores, "categoria_producto.ciclo_jerarquia", fake);
        Assert.Null(a.CategoriaPadreId);
    }

    [Fact]
    public async Task Handle_ReturnsFallo_WhenCicloDeTresNiveles()
    {
        // A ← B ← C (C es hija de B, B es hija de A); asignar C como padre de A cerraría A→C→B→A.
        var fake = new CategoriaProductoRepoFake();
        var a = fake.Agregar("A");
        var b = fake.Agregar("B", a.Id);
        var c = fake.Agregar("C", b.Id);
        var handler = new UpdateCategoriaProductoCommandHandler(fake.UnitOfWork.Object);

        var result = await handler.Handle(new UpdateCategoriaProductoCommand(a.Id, "A", "A", c.Id), default);

        AssertRechazadoSinGuardar(result.Errores, "categoria_producto.ciclo_jerarquia", fake);
        Assert.Null(a.CategoriaPadreId);
    }

    [Fact]
    public async Task Handle_PermiteMoverAUnaRamaDistinta_SinFalsosCiclos()
    {
        // Raíz ← A ← B y Raíz ← C: mover B bajo C es válido.
        var fake = new CategoriaProductoRepoFake();
        var raiz = fake.Agregar("RAIZ");
        var a = fake.Agregar("A", raiz.Id);
        var b = fake.Agregar("B", a.Id);
        var c = fake.Agregar("C", raiz.Id);
        var handler = new UpdateCategoriaProductoCommandHandler(fake.UnitOfWork.Object);

        var result = await handler.Handle(new UpdateCategoriaProductoCommand(b.Id, "B", "B", c.Id), default);

        Assert.True(result.EsExito);
        Assert.Equal(c.Id, b.CategoriaPadreId);
    }

    [Fact]
    public async Task Handle_NoSeCuelgaSiLaBaseYaContieneUnCicloAjeno()
    {
        // X ↔ Y ya forman un ciclo (dato corrupto); editar Z para colgarla de X no debe girar en falso.
        var fake = new CategoriaProductoRepoFake();
        var x = fake.Agregar("X");
        var y = fake.Agregar("Y", x.Id);
        x.CategoriaPadreId = y.Id;
        var z = fake.Agregar("Z");
        var handler = new UpdateCategoriaProductoCommandHandler(fake.UnitOfWork.Object);

        var result = await handler.Handle(new UpdateCategoriaProductoCommand(z.Id, "Z", "Z", x.Id), default);

        Assert.True(result.EsExito);
    }

    [Fact]
    public async Task Handle_ReturnsFallo_WhenDatosInvalidos()
    {
        var fake = new CategoriaProductoRepoFake();
        var handler = new UpdateCategoriaProductoCommandHandler(fake.UnitOfWork.Object);

        var result = await handler.Handle(new UpdateCategoriaProductoCommand(Guid.NewGuid(), "", "", null), default);

        Assert.True(result.EsFallo);
        Assert.Equal(2, result.Errores.Count);
        fake.Repo.Verify(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static void AssertRechazadoSinGuardar(
        IReadOnlyList<OpenSource1.Core.Common.Error> errores, string codigoEsperado, CategoriaProductoRepoFake fake)
    {
        var error = Assert.Single(errores);
        Assert.Equal(codigoEsperado, error.Codigo);
        Assert.Equal("CategoriaPadreId", error.Campo);
        fake.Repo.Verify(r => r.Update(It.IsAny<OpenSource1.Core.Entities.CategoriaProducto>()), Times.Never);
        fake.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
