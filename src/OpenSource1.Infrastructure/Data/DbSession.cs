using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Npgsql;
using OpenSource1.Application.Data;

namespace OpenSource1.Infrastructure.Data;

public sealed class DbSession : IDbSession, IDisposable
{
    private readonly NpgsqlConnection _connection;
    private NpgsqlTransaction? _transaction;
    private bool _disposed;

    public DbSession(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        // Se crea sin abrir: EF Core la abre cuando la necesita y Dapper reutiliza la misma instancia.
        _connection = new NpgsqlConnection(connectionString);
    }

    public DbConnection Connection => _connection;
    public DbTransaction? CurrentTransaction => _transaction;
    public bool HayTransaccionActiva => _transaction is not null;

    public async Task EnsureOpenAsync(CancellationToken cancellationToken = default)
    {
        if (_connection.State != ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }
    }

    public async Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            // Transacción anidada: se une a la existente y no hace commit al salir.
            return new AmbitoAnidado();
        }

        await EnsureOpenAsync(cancellationToken);
        _transaction = await _connection.BeginTransactionAsync(cancellationToken);
        return new AmbitoRaiz(this);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_transaction is not null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        await _connection.DisposeAsync();
    }

    /// <summary>
    /// Soporte para disposal síncrono (p. ej. un <c>using var</c> fuera de un contexto async).
    /// Bloquea sobre <see cref="DisposeAsync"/> en vez de duplicar la lógica de limpieza; siempre
    /// que haya un contexto async disponible, prefiera <c>await using</c> con
    /// <see cref="DisposeAsync"/> directamente para no bloquear un hilo.
    /// </summary>
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private sealed class AmbitoAnidado : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class AmbitoRaiz(DbSession sesion) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            // Si nadie hizo commit, deshacer. Protege contra excepciones a medio camino.
            await sesion.RollbackAsync();
        }
    }
}
