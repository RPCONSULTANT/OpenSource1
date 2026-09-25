using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenSource1.Application.Data;
using OpenSource1.Infrastructure.Data;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Infrastructure;

/// <summary>
/// Prueba de la propiedad central de <see cref="IDbSession"/>: EF Core y Dapper comparten la
/// misma conexión física y transacción, de modo que una escritura EF sin commit es visible
/// para una lectura Dapper en el mismo scope, y deja de serlo tras el rollback. Es la propiedad
/// que las conexiones separadas de antes (<c>IDbConnectionFactory</c>, una <c>NpgsqlConnection</c>
/// nueva por llamada) no podían cumplir.
///
/// REQUIERE DOCKER: usa <see cref="PostgresTestFixture"/>, que levanta un contenedor
/// postgres:17-alpine real. Si el daemon de Docker no está disponible, la inicialización de la
/// fixture falla y los tests se reportan como fallidos (no como "skip") — nunca como falso verde.
///
/// Una cosa a propósito de cómo está escrito este archivo, por un bug preexistente y no
/// relacionado con Task 1.5 (IDbSession), descubierto al intentar correrlo de verdad contra
/// Docker en este entorno:
///
/// No pasa por <c>WebApplicationFactory&lt;Program&gt;</c> / <c>PostgresTestFixture.CreateFactory()</c>
/// (el camino de <c>OpenSource1.SmokeTests.Api</c>). Ese camino arranca <c>Program.cs</c> completo, y
/// <c>AddApplicationIdentity</c> valida "eager" (antes del <c>Build()</c> del host, antes de que las
/// sobrescrituras de configuración del test se apliquen) que <c>Jwt:SigningKey</c> tenga 32+
/// caracteres. El placeholder de <c>appsettings.json</c> ("__SET_IN_USER_SECRETS__") no llega a
/// 32, así que revienta con <c>InvalidOperationException</c> antes de levantar nada. Se reproduce
/// igual, sin tocar nada de esta tarea, en <c>AppSettingsApiTests</c> (ya existente). Por eso aquí
/// se construye directamente el árbol de DI de
/// <c>OpenSource1.Infrastructure.Data.DependencyInjection.AddApplicationData</c> — exactamente la
/// porción que Task 1.5 cambia — sin arrastrar Identity/JWT.
///
/// La escritura de prueba tampoco usa <see cref="ApplicationDbContext"/> con la entidad
/// <c>Entrada</c> real; en su lugar usa un <see cref="DbContext"/> mínimo y propio de este test
/// (<see cref="ProbeDbContext"/>), configurado exactamente igual que el registro real de Step 3
/// (<c>UseNpgsql(session.Connection)</c>), contra su propia tabla. La propiedad que Task 1.5
/// introduce — misma conexión, misma transacción, visible para Dapper — es agnóstica al modelo de
/// entidades: se cumple o no se cumple independientemente de si el modelo es Entrada o esta tabla
/// de prueba. (El bug histórico de constructor sin enlazar en <c>DireccionCliente</c> que en su
/// momento impedía construir <c>ApplicationDbContext.Model</c> se corrigió con el patrón factory +
/// constructor privado de la Task de estandarización de value objects; ver
/// <see cref="ApplicationDbContextModelTests"/>, que ejerce ese modelo completo directamente.) El
/// cableado real de Step 3 (que <see cref="ApplicationDbContext"/> tal cual lo resuelve el
/// contenedor de producción efectivamente usa <see cref="IDbSession.Connection"/>) se verifica
/// aparte, por referencia, en <see cref="ApplicationDbContext_UsaLaMismaConexionQueElDbSession"/>.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DbSessionTests : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private readonly PostgresTestFixture _fixture;
    private ServiceProvider _provider = null!;

    public DbSessionTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _fixture.AppConnectionString,
                ["ConnectionStrings:IdentityConnection"] = _fixture.IdentityConnectionString,
                ["Database:ApplyMigrationsOnStartup"] = "false",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddApplicationData(configuration);
        _provider = services.BuildServiceProvider();

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task ApplicationDbContext_UsaLaMismaConexionQueElDbSession()
    {
        // Verifica el cableado exacto de Step 3 sin tocar ApplicationDbContext.Model (resolver el
        // DbContext o llamar Database.GetDbConnection() en él fuerza la construcción del modelo
        // internamente, que es una operación aparte de lo que esta prueba quiere aislar). En su
        // lugar se inspecciona, sin construir el DbContext, las DbContextOptions<ApplicationDbContext>
        // tal como las arma el registro real de Step 3
        // (options.UseNpgsql(sp.GetRequiredService<DbSession>().Connection)): que la conexión que
        // EF tiene configurada es, por referencia, la misma que expone IDbSession del mismo scope.
        await using var scope = _provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDbSession>();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>();

        var relationalExtension = options.Extensions.OfType<RelationalOptionsExtension>().Single();

        Assert.Same(session.Connection, relationalExtension.Connection);
    }

    [Fact]
    public async Task LecturaDapper_VeEscrituraEfDentroDeLaMismaTransaccion()
    {
        await using var scope = _provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDbSession>();

        await session.EnsureOpenAsync();
        await using var probeContext = CrearProbeDbContext(session);
        await probeContext.Database.EnsureCreatedAsync();

        await using var tx = await session.BeginTransactionAsync();
        await probeContext.Database.UseTransactionAsync(session.CurrentTransaction);

        var item = new ProbeEntity { Nombre = "T" };
        probeContext.Items.Add(item);
        await probeContext.SaveChangesAsync();

        var encontrado = await session.Connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(
                "SELECT \"Id\" FROM \"DbSessionProbe\" WHERE \"Id\" = @Id",
                new { item.Id }, session.CurrentTransaction));

        Assert.Equal(item.Id, encontrado); // con IDbConnectionFactory (conexiones separadas) esto fallaba

        await session.RollbackAsync();
    }

    [Fact]
    public async Task LecturaDapper_NoVeEscrituraEfTrasRollback()
    {
        Guid itemId;

        await using (var scope = _provider.CreateAsyncScope())
        {
            var session = scope.ServiceProvider.GetRequiredService<IDbSession>();

            await session.EnsureOpenAsync();
            await using var probeContext = CrearProbeDbContext(session);
            await probeContext.Database.EnsureCreatedAsync();

            await using (await session.BeginTransactionAsync())
            {
                await probeContext.Database.UseTransactionAsync(session.CurrentTransaction);

                var item = new ProbeEntity { Nombre = "T2" };
                probeContext.Items.Add(item);
                await probeContext.SaveChangesAsync();
                itemId = item.Id;

                await session.RollbackAsync();
            }
        }

        // Scope nuevo, conexión nueva: confirma que el rollback fue real y no un artefacto de
        // reutilizar la misma conexión/transacción todavía viva.
        await using var verificationScope = _provider.CreateAsyncScope();
        var verificationSession = verificationScope.ServiceProvider.GetRequiredService<IDbSession>();
        await verificationSession.EnsureOpenAsync();

        var encontrado = await verificationSession.Connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(
                "SELECT \"Id\" FROM \"DbSessionProbe\" WHERE \"Id\" = @Id",
                new { Id = itemId }));

        Assert.Null(encontrado);
    }

    [Fact]
    public async Task Dispose_Sincrono_CierraLaConexionSinLanzar()
    {
        // Hallazgo 5: DbSession solo implementaba IAsyncDisposable. Un "using var" síncrono
        // (patrón común en un BackgroundService o un scope creado fuera de un contexto async)
        // lanzaría al no encontrar Dispose(). Se construye la sesión directamente (fuera del
        // contenedor de DI) para poder invocar Dispose() de forma síncrona de manera aislada,
        // sin interferir con el ciclo de vida async que gestiona el scope del contenedor.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _fixture.AppConnectionString,
            })
            .Build();

        var session = new DbSession(configuration);
        await session.EnsureOpenAsync();
        Assert.Equal(System.Data.ConnectionState.Open, session.Connection.State);

        // Dispose síncrono (no DisposeAsync): antes del fix no compilaba/lanzaba porque
        // IDbSession/DbSession no implementaban IDisposable.
        ((IDisposable)session).Dispose();

        Assert.Equal(System.Data.ConnectionState.Closed, session.Connection.State);

        // Idempotente: una segunda llamada (síncrona o asíncrona) no debe lanzar.
        ((IDisposable)session).Dispose();
        await session.DisposeAsync();
    }

    private static ProbeDbContext CrearProbeDbContext(IDbSession session)
    {
        var options = new DbContextOptionsBuilder<ProbeDbContext>()
            .UseNpgsql(session.Connection)
            .Options;
        return new ProbeDbContext(options);
    }

    private sealed class ProbeEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public required string Nombre { get; set; }
    }

    private sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options) : DbContext(options)
    {
        public DbSet<ProbeEntity> Items => Set<ProbeEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ProbeEntity>(entity =>
            {
                entity.ToTable("DbSessionProbe");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
            });
        }
    }
}
