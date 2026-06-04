using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenSource1.Infrastructure.Identity;

namespace OpenSource1.Infrastructure.Data;

public sealed class DatabaseMigrationHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<DatabaseOptions> databaseOptions,
    ILogger<DatabaseMigrationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!databaseOptions.Value.ApplyMigrationsOnStartup)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();

        logger.LogInformation("Applying ApplicationDbContext migrations.");
        var applicationDbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await MigrateAsync(applicationDbContext.Database, cancellationToken);

        logger.LogInformation("Applying AppIdentityDbContext migrations.");
        var identityDbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        await MigrateAsync(identityDbContext.Database, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task MigrateAsync(DatabaseFacade database, CancellationToken cancellationToken)
    {
        try
        {
            await database.MigrateAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P04") // database already exists
        {
            logger.LogWarning(ex, "PostgreSQL database already exists. Retrying migrations.");
            await database.MigrateAsync(cancellationToken);
        }
    }
}
