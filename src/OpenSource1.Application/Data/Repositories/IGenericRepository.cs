using System.Linq.Expressions;
using OpenSource1.Core.Abstractions;

namespace OpenSource1.Application.Data.Repositories;

public interface IGenericRepository<TEntity>
    where TEntity : class, IAggregateRoot
{
    IQueryable<TEntity> Query(bool asTracking = false);
    Task<TEntity?> GetByIdAsync(object[] keyValues, CancellationToken cancellationToken = default);
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, bool asTracking = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);
}
