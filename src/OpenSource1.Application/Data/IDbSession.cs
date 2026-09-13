using System.Data.Common;

namespace OpenSource1.Application.Data;

/// <summary>
/// Posee la única conexión física del scope. EF Core y Dapper la comparten, de modo que
/// una transacción abierta aquí es visible para ambos.
/// </summary>
/// <summary>
/// También implementa <see cref="IDisposable"/> (además de <see cref="IAsyncDisposable"/>) para
/// soportar un <c>using</c> síncrono (p. ej. un <c>BackgroundService</c> o un scope creado fuera
/// de un contexto async). La implementación bloquea sobre el camino async: prefiera siempre
/// <see cref="IAsyncDisposable.DisposeAsync"/> cuando haya un contexto async disponible.
/// </summary>
public interface IDbSession : IAsyncDisposable, IDisposable
{
    DbConnection Connection { get; }
    DbTransaction? CurrentTransaction { get; }
    bool HayTransaccionActiva { get; }

    Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
    Task EnsureOpenAsync(CancellationToken cancellationToken = default);
}
