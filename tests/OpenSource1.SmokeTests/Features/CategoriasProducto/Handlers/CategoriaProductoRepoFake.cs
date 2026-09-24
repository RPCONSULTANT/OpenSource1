using System.Linq.Expressions;
using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Core.Entities;

namespace OpenSource1.SmokeTests.Features.CategoriasProducto.Handlers;

/// <summary>
/// Repositorio simulado en memoria para probar la jerarquía sin base de datos. Imita el filtro
/// global de EF (<c>!IsDeleted</c>): los registros borrados lógicamente no se devuelven.
/// </summary>
internal sealed class CategoriaProductoRepoFake
{
    private readonly Dictionary<Guid, CategoriaProducto> _categorias = [];

    public CategoriaProductoRepoFake()
    {
        Repo = new Mock<IGenericRepository<CategoriaProducto>>();
        Repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object[] keys, CancellationToken _) =>
                _categorias.GetValueOrDefault((Guid)keys[0]) is { IsDeleted: false } c ? c : null);
        Repo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<CategoriaProducto, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<CategoriaProducto, bool>> predicate, bool _, CancellationToken _) =>
                _categorias.Values.Where(c => !c.IsDeleted).FirstOrDefault(predicate.Compile()));
        Repo.Setup(r => r.AddAsync(It.IsAny<CategoriaProducto>(), It.IsAny<CancellationToken>()))
            .Callback<CategoriaProducto, CancellationToken>((c, _) => _categorias[c.Id] = c)
            .Returns(Task.CompletedTask);

        UnitOfWork = new Mock<IUnitOfWork>();
        UnitOfWork.Setup(u => u.Repository<CategoriaProducto>()).Returns(Repo.Object);
        UnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    public Mock<IGenericRepository<CategoriaProducto>> Repo { get; }
    public Mock<IUnitOfWork> UnitOfWork { get; }

    /// <summary>Borrado lógico: a partir de aquí el repositorio simulado no la devuelve.</summary>
    public void Borrar(CategoriaProducto categoria) => categoria.IsDeleted = true;

    public CategoriaProducto Agregar(string codigo, Guid? padreId = null)
    {
        var categoria = new CategoriaProducto { Codigo = codigo, Nombre = $"Nombre {codigo}", CategoriaPadreId = padreId };
        _categorias[categoria.Id] = categoria;
        return categoria;
    }
}
