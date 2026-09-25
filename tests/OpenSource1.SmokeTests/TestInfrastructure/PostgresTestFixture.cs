using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenSource1.Api;

namespace OpenSource1.SmokeTests.TestInfrastructure;

/// <summary>
/// Agrupa todas las clases de test que usan <see cref="PostgresTestFixture"/> en una única
/// colección xUnit con <c>DisableParallelization = true</c>, de modo que se ejecuten en
/// serie en vez de competir por el mismo nombre de contenedor Docker y el mismo puerto fijos
/// (<c>opensource1-tests-postgres</c>, 65432). Cada clase conserva su propia instancia del
/// fixture vía <c>IClassFixture&lt;PostgresTestFixture&gt;</c> (un contenedor propio, creado y
/// destruido por clase); esta colección solo serializa el orden de ejecución entre clases.
/// Decorar cada clase que consuma el fixture con <c>[Collection(Name)]</c>.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresCollection
{
    public const string Name = "Postgres";
}

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

        // Cada clase de test respawnea un contenedor nuevo pero reutiliza exactamente la misma
        // cadena de conexión (mismo host/puerto/db/usuario/password, por diseño: el puerto y el
        // nombre de contenedor son constantes). Npgsql mantiene un pool de conexiones físicas a
        // nivel de proceso, indexado por el texto exacto de la cadena de conexión — no por
        // contenedor. Sin este ClearAllPools(), una conexión pooled abierta contra el contenedor
        // anterior (ya destruido por el "docker rm -f" de arriba, o el de la clase previa) puede
        // devolverse como si estuviera sana, y la primera operación sobre ella revienta con
        // EndOfStreamException / "Exception while reading from stream" al intentar leer de un
        // socket cuyo proceso servidor ya no existe. Limpiar los pools al iniciar cada fixture
        // garantiza que la próxima conexión abierta sea física y nueva, contra el contenedor que
        // se acaba de levantar.
        NpgsqlConnection.ClearAllPools();

        await RunAsync("docker", $"run -d --name {ContainerName} -e POSTGRES_PASSWORD={_password} -p {HostPort}:5432 postgres:17-alpine");
        await WaitForPostgresAsync();
        await EnsureDatabaseAsync("AxionERP_App");
        await EnsureDatabaseAsync("AxionERP_Identity");
    }

    public async Task DisposeAsync()
    {
        await RunAsync("docker", $"rm -f {ContainerName}", ignoreFailure: true);

        // Evita que conexiones pooled contra el contenedor recién destruido sobrevivan para la
        // siguiente clase (ver comentario en InitializeAsync).
        NpgsqlConnection.ClearAllPools();
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
        // "-h 127.0.0.1" fuerza a pg_isready a comprobar el listener TCP, no el socket Unix.
        // La imagen oficial de postgres arranca un "servidor temporal" interno (para correr
        // scripts de inicialización) con listen_addresses='' — solo acepta el socket Unix,
        // nunca TCP — y lo apaga antes de levantar el servidor real. Sin "-h" aquí, pg_isready
        // puede reportar listo contra ese servidor temporal (ventana confirmada de ~100ms en la
        // imagen postgres:17-alpine), y una consulta inmediatamente posterior falla con
        // "the database system is shutting down". Comprobar el TCP evita esa ventana: el
        // servidor temporal nunca escucha ahí, así que solo puede responder "accepting
        // connections" el servidor real definitivo.
        for (var i = 0; i < 60; i++)
        {
            var result = await RunAsync("docker", $"exec {ContainerName} pg_isready -U postgres -h 127.0.0.1", captureOutput: true, ignoreFailure: true);
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
                    ["Jwt:ExpirationMinutes"] = "60",
                    ["Cors:AllowedOrigins:0"] = "http://localhost:5110"
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
