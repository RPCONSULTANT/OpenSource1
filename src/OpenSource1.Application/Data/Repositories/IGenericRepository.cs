using System.Linq.Expressions;
using OpenSource1.Core.Abstractions;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Data.Repositories;

public interface IGenericRepository<TEntity>
    where TEntity : class, IAggregateRoot
{
    IQueryable<TEntity> Query(bool asTracking = false);
    Task<TEntity?> GetByIdAsync(object[] keyValues, CancellationToken cancellationToken = default);
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, bool asTracking = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Variante paginada de <see cref="ListAsync"/>. Implementada con un <c>CountAsync</c> más un
    /// <c>Skip</c>/<c>Take</c> sobre la misma consulta, ambos ejecutados contra la conexión del
    /// scope actual. <see cref="ListAsync"/> sin paginar se conserva para el uso interno de los
    /// handlers de escritura (verificaciones de existencia, etc.).
    /// </summary>
    Task<PagedResult<TEntity>> ListPagedAsync(
        Expression<Func<TEntity, bool>>? predicate,
        PageRequest paginacion,
        CancellationToken cancellationToken = default);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);
}
