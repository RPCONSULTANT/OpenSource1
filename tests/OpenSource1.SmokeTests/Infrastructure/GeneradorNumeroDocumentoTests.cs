using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenSource1.Application.Data;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Enums;
using OpenSource1.Infrastructure.Data;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Infrastructure;

/// <summary>
/// Prueba de la propiedad central de <see cref="GeneradorNumeroDocumento"/>: el <c>SELECT ...
/// FOR UPDATE</c> sobre <c>LineasSerie</c> serializa de verdad transacciones concurrentes, de
/// modo que 10 llamadas simultáneas a <see cref="IGeneradorNumeroDocumento.SiguienteAsync"/>
/// contra la misma línea producen 10 números distintos y consecutivos, sin huecos ni
/// duplicados. Un mock no puede probar esto — la garantía depende del comportamiento real de
/// bloqueo de filas de PostgreSQL bajo transacciones concurrentes genuinas.
///
/// REQUIERE DOCKER: usa <see cref="PostgresTestFixture"/>, igual que <c>DbSessionTests</c>. Por
/// el mismo motivo documentado allí (validación eager de <c>Jwt:SigningKey</c> en
/// <c>Program.cs</c> antes de que la configuración de test se aplique), este archivo tampoco
/// pasa por <c>WebApplicationFactory&lt;Program&gt;</c>: construye directamente el árbol de DI de
/// <c>AddApplicationData</c> y aplica las migraciones reales (incluida
/// <c>AddSeriesNumeracion</c>) contra el contenedor con una conexión EF propia, independiente de
/// los <see cref="IDbSession"/> de cada scope de prueba.
///
/// Cada una de las 10 "tareas concurrentes" del test de concurrencia abre su propio
/// <c>IServiceScope</c> — y por tanto su propio <see cref="IDbSession"/> con su propia conexión
/// física <c>NpgsqlConnection</c> y su propia transacción — nunca comparten una conexión entre
/// sí. Eso es lo que hace la concurrencia genuina en vez de simulada.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class GeneradorNumeroDocumentoTests : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private readonly PostgresTestFixture _fixture;
    private ServiceProvider _provider = null!;

    public GeneradorNumeroDocumentoTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
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

        var migrationOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_fixture.AppConnectionString)
            .Options;
        await using var migrationContext = new ApplicationDbContext(migrationOptions);
        await migrationContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task SiguienteAsync_ConcurrenciaReal_NoProduceNumerosDuplicados()
    {
        var codigoSerie = $"CONC{Guid.NewGuid():N}"[..12];
        var fecha = new DateOnly(2026, 1, 1);
        await SembrarSerieAsync(codigoSerie, fecha);

        const int tareas = 10;
        const int demoraCriticaMs = 150;

        var resultados = new ConcurrentBag<Result<string>>();
        var cronometro = Stopwatch.StartNew();

        var trabajos = Enumerable.Range(0, tareas).Select(async _ =>
        {
            // Scope propio => IDbSession propio => NpgsqlConnection física propia. Ninguna de las
            // 10 tareas comparte conexión ni transacción con otra.
            await using var scope = _provider.CreateAsyncScope();
            var session = scope.ServiceProvider.GetRequiredService<IDbSession>();
            var generador = scope.ServiceProvider.GetRequiredService<IGeneradorNumeroDocumento>();

            await session.EnsureOpenAsync();
            await using var tx = await session.BeginTransactionAsync();

            var resultado = await generador.SiguienteAsync(codigoSerie, fecha);

            // Espera deliberada SOLO DE PRUEBA dentro de la sección crítica (mientras la
            // transacción sigue abierta, el FOR UPDATE de arriba sigue reteniendo el bloqueo de
            // fila). Nunca en código de producción. Sin esta espera, 10 llamadas contra un
            // Postgres en localhost son tan rápidas (~1-2ms de round-trip) que rara vez llegan a
            // solaparse de verdad: cada transacción podría encontrar la fila ya liberada por el
            // commit anterior sin haber tenido que esperar nunca en la cola del bloqueo, y la
            // prueba dejaría de demostrar contención real aunque el resultado final (sin huecos,
            // sin duplicados) fuera casualmente correcto. Al retener el bloqueo con una espera
            // fija tras adquirirlo, se obliga a que las otras 9 transacciones hagan cola real en
            // el FOR UPDATE mientras esta sigue abierta — y el tiempo total medido más abajo lo
            // confirma.
            await Task.Delay(demoraCriticaMs);

            await session.CommitAsync();
            resultados.Add(resultado);
        });

        await Task.WhenAll(trabajos);
        cronometro.Stop();

        Assert.Equal(tareas, resultados.Count);
        Assert.All(resultados, r => Assert.True(
            r.EsExito,
            r.EsFallo ? string.Join("; ", r.Errores.Select(e => $"{e.Codigo}: {e.Mensaje}")) : string.Empty));

        var numeros = resultados.Select(r => r.Valor).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var esperados = Enumerable.Range(1, tareas).Select(n => n.ToString().PadLeft(5, '0')).ToList();

        // Consecutivos y sin huecos: exactamente 00001..00010, cada uno una sola vez.
        Assert.Equal(esperados, numeros);
        Assert.Equal(tareas, numeros.Distinct(StringComparer.Ordinal).Count());

        // Evidencia de serialización real: si las 10 secciones críticas (cada una reteniendo el
        // bloqueo demoraCriticaMs) hubieran corrido sin contención, el tiempo total habría sido
        // ~demoraCriticaMs (las 10 en paralelo, sin esperar unas a otras). Al estar serializadas
        // por el FOR UPDATE, el tiempo total debe acercarse a tareas * demoraCriticaMs.
        var minimoEsperadoMs = (tareas - 1) * demoraCriticaMs;
        Assert.True(
            cronometro.ElapsedMilliseconds >= minimoEsperadoMs,
            $"Tiempo total ({cronometro.ElapsedMilliseconds}ms) sugiere que las transacciones NO se serializaron " +
            $"(se esperaba >= {minimoEsperadoMs}ms si el FOR UPDATE realmente puso a las demás en cola).");
    }

    [Fact]
    public async Task SiguienteAsync_SinTransaccionActiva_DevuelveFallo()
    {
        var codigoSerie = $"SINTX{Guid.NewGuid():N}"[..12];
        await SembrarSerieAsync(codigoSerie, new DateOnly(2026, 1, 1));

        await using var scope = _provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDbSession>();
        var generador = scope.ServiceProvider.GetRequiredService<IGeneradorNumeroDocumento>();

        await session.EnsureOpenAsync();

        var resultado = await generador.SiguienteAsync(codigoSerie, new DateOnly(2026, 1, 1));

        Assert.True(resultado.EsFallo);
        Assert.Equal("numeracion.sin_transaccion", resultado.Errores[0].Codigo);
    }

    [Fact]
    public async Task SiguienteAsync_SerieInexistente_DevuelveFallo()
    {
        await using var scope = _provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDbSession>();
        var generador = scope.ServiceProvider.GetRequiredService<IGeneradorNumeroDocumento>();

        await session.EnsureOpenAsync();
        await using var tx = await session.BeginTransactionAsync();

        var resultado = await generador.SiguienteAsync("NO-EXISTE", new DateOnly(2026, 1, 1));

        Assert.True(resultado.EsFallo);
        Assert.Equal("numeracion.serie_inexistente", resultado.Errores[0].Codigo);

        await session.RollbackAsync();
    }

    [Fact]
    public async Task SiguienteAsync_SinLineaVigenteParaLaFecha_DevuelveFallo()
    {
        var codigoSerie = $"SINLIN{Guid.NewGuid():N}"[..12];
        await SembrarSerieAsync(codigoSerie, new DateOnly(2026, 6, 1));

        await using var scope = _provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDbSession>();
        var generador = scope.ServiceProvider.GetRequiredService<IGeneradorNumeroDocumento>();

        await session.EnsureOpenAsync();
        await using var tx = await session.BeginTransactionAsync();

        // La línea sembrada solo es vigente a partir de 2026-06-01.
        var resultado = await generador.SiguienteAsync(codigoSerie, new DateOnly(2026, 1, 1));

        Assert.True(resultado.EsFallo);
        Assert.Equal("numeracion.sin_linea_vigente", resultado.Errores[0].Codigo);

        await session.RollbackAsync();
    }

    [Fact]
    public async Task SiguienteAsync_SerieAgotada_DevuelveFallo()
    {
        var codigoSerie = $"AGOT{Guid.NewGuid():N}"[..12];
        var fecha = new DateOnly(2026, 1, 1);

        // Rango de un solo número, ya usado: el siguiente (00002) excede NumeroFinal (00001).
        await SembrarSerieAsync(codigoSerie, fecha, numeroInicial: "00001", numeroFinal: "00001", ultimoNumeroUsado: "00001");

        await using var scope = _provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDbSession>();
        var generador = scope.ServiceProvider.GetRequiredService<IGeneradorNumeroDocumento>();

        await session.EnsureOpenAsync();
        await using var tx = await session.BeginTransactionAsync();

        var resultado = await generador.SiguienteAsync(codigoSerie, fecha);

        Assert.True(resultado.EsFallo);
        Assert.Equal("numeracion.serie_agotada", resultado.Errores[0].Codigo);

        await session.RollbackAsync();
    }

    private async Task SembrarSerieAsync(
        string codigo,
        DateOnly fechaInicial,
        string numeroInicial = "00001",
        string numeroFinal = "00100",
        string ultimoNumeroUsado = "00000")
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_fixture.AppConnectionString)
            .Options;

        await using var context = new ApplicationDbContext(options);

        var serie = new Serie
        {
            Codigo = codigo,
            Descripcion = "Serie de prueba de numeración",
            TipoDocumento = TipoDocumentoSerie.DiarioInventario,
            PermiteHuecos = false,
        };
        context.Series.Add(serie);
        await context.SaveChangesAsync();

        var linea = new LineaSerie
        {
            SerieId = serie.Id,
            NumeroInicial = numeroInicial,
            NumeroFinal = numeroFinal,
            UltimoNumeroUsado = ultimoNumeroUsado,
            FechaInicial = fechaInicial,
            Incremento = 1,
            Bloqueada = false,
        };
        context.LineasSerie.Add(linea);
        await context.SaveChangesAsync();
    }
}
