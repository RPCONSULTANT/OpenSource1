using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using OpenSource1.Core.Abstractions;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Infrastructure.Data;

namespace OpenSource1.Infrastructure.Data.UnitOfWork;

public sealed class UnitOfWork(ApplicationDbContext dbContext, IServiceProvider serviceProvider) : IUnitOfWork
{
    public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class, IAggregateRoot =>
        serviceProvider.GetRequiredService<IGenericRepository<TEntity>>();

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditValues();
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditValues()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in dbContext.ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;

                if (string.IsNullOrWhiteSpace(entry.Entity.CreatedBy))
                {
                    entry.Entity.CreatedBy = "system";
                }
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
                entry.Entity.UpdatedBy = "system";
                entry.Property(entity => entity.CreatedAtUtc).IsModified = false;
                entry.Property(entity => entity.CreatedBy).IsModified = false;
            }
        }
    }
}
