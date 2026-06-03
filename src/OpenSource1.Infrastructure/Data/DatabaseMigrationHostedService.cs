using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
        await applicationDbContext.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Applying AppIdentityDbContext migrations.");
        var identityDbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        await identityDbContext.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
