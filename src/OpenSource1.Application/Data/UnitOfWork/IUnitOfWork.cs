using OpenSource1.Core.Abstractions;
using OpenSource1.Application.Data.Repositories;

namespace OpenSource1.Application.Data.UnitOfWork;

public interface IUnitOfWork
{
    IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class, IAggregateRoot;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
