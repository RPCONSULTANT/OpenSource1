using test.Data.Abstractions;
using test.Data.Repositories;

namespace test.Data.UnitOfWork;

public interface IUnitOfWork
{
    IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class, IAggregateRoot;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
