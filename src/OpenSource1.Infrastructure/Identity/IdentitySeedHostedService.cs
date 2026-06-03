using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenSource1.Application.Security;

namespace OpenSource1.Infrastructure.Identity;

public sealed class IdentitySeedHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<UserSeedOptions> seedOptions,
    ILogger<IdentitySeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!seedOptions.Value.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(seedOptions.Value.DefaultPassword))
        {
            throw new InvalidOperationException("UserSeed:DefaultPassword must be configured with a secure environment variable when user seeding is enabled.");
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();

        foreach (var role in ApplicationRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await EnsureUserAsync(userManager, "admin", "admin@opensource1.local", "Administrador", ApplicationRoles.Administrator, seedOptions.Value.DefaultPassword);
        await EnsureUserAsync(userManager, "supervisor", "supervisor@opensource1.local", "Supervisor", ApplicationRoles.Supervisor, seedOptions.Value.DefaultPassword);
        await EnsureUserAsync(userManager, "ejecutor", "ejecutor@opensource1.local", "Ejecutor", ApplicationRoles.Executor, seedOptions.Value.DefaultPassword);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureUserAsync(
        UserManager<Usuario> userManager,
        string userName,
        string email,
        string fullName,
        string role,
        string password)
    {
        var user = await userManager.FindByNameAsync(userName);

        if (user is null)
        {
            user = new Usuario
            {
                UserName = userName,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to seed user '{userName}': {string.Join(", ", result.Errors.Select(error => error.Description))}");
            }

            logger.LogInformation("Seeded local user {UserName}.", userName);
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}
