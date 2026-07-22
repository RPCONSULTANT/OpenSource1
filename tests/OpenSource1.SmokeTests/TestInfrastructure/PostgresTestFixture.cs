using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenSource1.Api;

namespace OpenSource1.SmokeTests.TestInfrastructure;

public sealed class PostgresTestFixture : IAsyncLifetime
{
    private const string ContainerName = "opensource1-tests-postgres";
    private const int HostPort = 65432;
    private readonly string _password = "Change_this_postgres_password_12345";

    public string AppConnectionString => $"Host=localhost;Port={HostPort};Database=AxionERP_App;Username=postgres;Password={_password}";
    public string IdentityConnectionString => $"Host=localhost;Port={HostPort};Database=AxionERP_Identity;Username=postgres;Password={_password}";

    public async Task InitializeAsync()
    {
        await RunAsync("docker", $"rm -f {ContainerName}", ignoreFailure: true);
        await RunAsync("docker", $"run -d --name {ContainerName} -e POSTGRES_PASSWORD={_password} -p {HostPort}:5432 postgres:17-alpine");
        await WaitForPostgresAsync();
        await EnsureDatabaseAsync("AxionERP_App");
        await EnsureDatabaseAsync("AxionERP_Identity");
    }

    public async Task DisposeAsync()
    {
        await RunAsync("docker", $"rm -f {ContainerName}", ignoreFailure: true);
    }

    public WebApplicationFactory<Program> CreateFactory()
    {
        return new ApiFactory(this);
    }

    private async Task EnsureDatabaseAsync(string databaseName)
    {
        var sql = $"SELECT 1 FROM pg_database WHERE datname = '{databaseName}';";
        var result = await RunAsync("docker", $"exec {ContainerName} psql -U postgres -d postgres -tAc \"{sql}\"", captureOutput: true);
        if (result.Output.Trim() == "1")
        {
            return;
        }

        await RunAsync("docker", $"exec {ContainerName} psql -U postgres -d postgres -c \"CREATE DATABASE \\\"{databaseName}\\\";\"");
    }

    private async Task WaitForPostgresAsync()
    {
        for (var i = 0; i < 60; i++)
        {
            var result = await RunAsync("docker", $"exec {ContainerName} pg_isready -U postgres", captureOutput: true, ignoreFailure: true);
            if (result.ExitCode == 0)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        throw new TimeoutException("PostgreSQL test container did not become ready in time.");
    }

    private static async Task<CommandResult> RunAsync(string fileName, string arguments, bool captureOutput = false, bool ignoreFailure = false)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start {fileName}.");
        var output = captureOutput ? await process.StandardOutput.ReadToEndAsync() + await process.StandardError.ReadToEndAsync() : string.Empty;
        await process.WaitForExitAsync();

        if (process.ExitCode != 0 && !ignoreFailure)
        {
            throw new InvalidOperationException($"{fileName} {arguments} failed with exit code {process.ExitCode}: {output}");
        }

        return new CommandResult(process.ExitCode, output);
    }

    private sealed record CommandResult(int ExitCode, string Output);

    private sealed class ApiFactory(PostgresTestFixture fixture) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = fixture.AppConnectionString,
                    ["ConnectionStrings:IdentityConnection"] = fixture.IdentityConnectionString,
                    ["Database:ApplyMigrationsOnStartup"] = "true",
                    ["UserSeed:Enabled"] = "true",
                    ["UserSeed:DefaultPassword"] = "Password123",
                    ["Jwt:Issuer"] = "OpenSource1.Tests",
                    ["Jwt:Audience"] = "OpenSource1.Tests",
                    ["Jwt:SigningKey"] = "TestSigningKey_ChangeMe_1234567890",
                    ["Jwt:ExpirationMinutes"] = "60"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                    options.DefaultScheme = "Test";
                });
            });
        }
    }
}
