using System.Data.Common;

namespace OpenSource1.Application.Data;

/// <summary>
/// Posee la única conexión física del scope. EF Core y Dapper la comparten, de modo que
/// una transacción abierta aquí es visible para ambos.
/// </summary>
public interface IDbSession : IAsyncDisposable
{
    DbConnection Connection { get; }
    DbTransaction? CurrentTransaction { get; }
    bool HayTransaccionActiva { get; }

    Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
    Task EnsureOpenAsync(CancellationToken cancellationToken = default);
}
