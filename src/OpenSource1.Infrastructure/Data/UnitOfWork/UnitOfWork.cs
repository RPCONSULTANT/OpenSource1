using Microsoft.Extensions.DependencyInjection;
using OpenSource1.Core.Abstractions;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Infrastructure.Data;

namespace OpenSource1.Infrastructure.Data.UnitOfWork;

public sealed class UnitOfWork(ApplicationDbContext dbContext, IServiceProvider serviceProvider) : IUnitOfWork
{
    public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class, IAggregateRoot =>
        serviceProvider.GetRequiredService<IGenericRepository<TEntity>>();

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
