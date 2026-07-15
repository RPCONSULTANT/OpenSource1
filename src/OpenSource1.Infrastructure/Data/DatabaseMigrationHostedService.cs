using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenSource1.Infrastructure.Identity;

namespace OpenSource1.Infrastructure.Data;

public sealed class DatabaseMigrationHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<DatabaseOptions> databaseOptions,
    IHostEnvironment hostEnvironment,
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
        await DatabaseMigrationExecutor.MigrateWithRecoveryAsync(
            database,
            hostEnvironment.IsDevelopment() && databaseOptions.Value.RecreateOnPendingModelChangesInDevelopment,
            logger,
            cancellationToken);
    }
}
