using System.Linq.Expressions;
using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Core.Entities;

namespace OpenSource1.SmokeTests.TestInfrastructure;

/// <summary>
/// Repositorio simulado en memoria que EVALÚA los predicados de verdad (no devuelve un valor fijo), e imita el filtro global de EF
/// (<c>!IsDeleted</c>): los registros borrados lógicamente no se devuelven. Sirve para probar handlers sin base de datos.
/// </summary>
internal sealed class RepositorioEnMemoria<T> where T : BaseEntity
{
    public RepositorioEnMemoria()
    {
        Mock = new Mock<IGenericRepository<T>>();
        Mock.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object[] keys, CancellationToken _) =>
                Datos.FirstOrDefault(e => e.Id == (Guid)keys[0] && !e.IsDeleted));
        Mock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<T, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<T, bool>> predicado, bool _, CancellationToken _) =>
                Datos.Where(e => !e.IsDeleted).FirstOrDefault(predicado.Compile()));
        Mock.Setup(r => r.ListAsync(It.IsAny<Expression<Func<T, bool>>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<T, bool>>? predicado, CancellationToken _) =>
                (IReadOnlyList<T>)[.. Datos.Where(e => !e.IsDeleted).Where(predicado?.Compile() ?? (_ => true))]);
        Mock.Setup(r => r.AddAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
            .Callback<T, CancellationToken>((e, _) => Datos.Add(e))
            .Returns(Task.CompletedTask);
    }

    public Mock<IGenericRepository<T>> Mock { get; }
    public List<T> Datos { get; } = [];

    public IGenericRepository<T> Repo => Mock.Object;

    public T Agregar(T entidad)
    {
        Datos.Add(entidad);
        return entidad;
    }
}
