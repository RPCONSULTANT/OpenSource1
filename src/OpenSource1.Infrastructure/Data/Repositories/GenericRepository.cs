using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Core.Abstractions;
using OpenSource1.Infrastructure.Data;

namespace OpenSource1.Infrastructure.Data.Repositories;

public sealed class GenericRepository<TEntity>(ApplicationDbContext dbContext) : IGenericRepository<TEntity>
    where TEntity : class, IAggregateRoot
{
    private readonly DbSet<TEntity> _dbSet = dbContext.Set<TEntity>();

    public IQueryable<TEntity> Query(bool asTracking = false) =>
        asTracking ? _dbSet.AsQueryable() : _dbSet.AsNoTracking();

    public async Task<TEntity?> GetByIdAsync(object[] keyValues, CancellationToken cancellationToken = default) =>
        await _dbSet.FindAsync(keyValues, cancellationToken);

    public async Task<IReadOnlyList<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        var query = Query();

        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _dbSet.AddAsync(entity, cancellationToken);
    }

    public void Update(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _dbSet.Update(entity);
    }

    public void Remove(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _dbSet.Remove(entity);
    }
}
