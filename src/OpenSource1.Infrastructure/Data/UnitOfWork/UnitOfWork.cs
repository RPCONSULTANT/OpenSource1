using OpenSource1.Domain.Abstractions;
using OpenSource1.Application.Data.Repositories;

namespace OpenSource1.Application.Data.UnitOfWork;

public sealed class UnitOfWork(ApplicationDbContext dbContext, IServiceProvider serviceProvider) : IUnitOfWork
{
    public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class, IAggregateRoot =>
        serviceProvider.GetRequiredService<IGenericRepository<TEntity>>();

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
