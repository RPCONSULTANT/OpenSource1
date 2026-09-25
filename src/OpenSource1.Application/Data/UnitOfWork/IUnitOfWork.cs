using OpenSource1.Core.Abstractions;
using OpenSource1.Application.Data.Repositories;

namespace OpenSource1.Application.Data.UnitOfWork;

public interface IUnitOfWork
{
    IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class, IAggregateRoot;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Indica si ya hay una transacción activa en la sesión subyacente.</summary>
    bool HayTransaccionActiva { get; }

    /// <summary>
    /// Abre una transacción (o se une a la activa si ya hay una) y la enrola en el
    /// <c>DbContext</c>, de modo que EF Core y Dapper comparten la misma transacción física.
    /// </summary>
    Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Guarda los cambios pendientes y confirma la transacción activa.</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Deshace la transacción activa sin guardar los cambios pendientes.</summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
