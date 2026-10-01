using System.Collections.Concurrent;
using System.Diagnostics;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.Series.Commands;
using OpenSource1.Application.Features.Series.Dtos;
using OpenSource1.Application.Features.Series.Handlers;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Clientes;
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
///
/// Espera SOLO DE PRUEBA: cada tarea retiene el bloqueo (transacción abierta tras numerar) una
/// demora fija antes del commit. Sin ella, 10 llamadas contra un Postgres local son tan rápidas
/// que rara vez se solapan y la prueba dejaría de demostrar contención real; con ella, las demás
/// transacciones hacen cola en el <c>FOR UPDATE</c> y el tiempo total medido (≥ (tareas − 1) ×
/// demora) lo confirma. Nunca en código de producción.
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
        services.AddHttpContextAccessor();
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


    [Theory]
    [InlineData("", 5)]
    [InlineData("PX-", 5)]
    public async Task SiguienteAsync_ConcurrenciaReal_SinDuplicadosNiHuecos(string prefijo, int ancho)
    {
        var fecha = new DateOnly(2026, 1, 1);
        var (serieId, _) = await SembrarSerieAsync(fecha, numeroInicial: prefijo + "1".PadLeft(ancho, '0'),
            numeroFinal: prefijo + "9".PadLeft(ancho, '9'), ultimoNumeroUsado: "");

        const int tareas = 10;
        const int demoraCriticaMs = 150;
        var resultados = new ConcurrentBag<Result<NumeroGenerado>>();
        var cronometro = Stopwatch.StartNew();

        await Task.WhenAll(Enumerable.Range(0, tareas).Select(async _ =>
        {
            await using var scope = _provider.CreateAsyncScope();
            var session = scope.ServiceProvider.GetRequiredService<IDbSession>();
            var generador = scope.ServiceProvider.GetRequiredService<IGeneradorNumeroDocumento>();
            await session.EnsureOpenAsync();
            await using var tx = await session.BeginTransactionAsync();
            var resultado = await generador.SiguienteAsync(serieId, TipoDocumentoSerie.DiarioInventario, fecha);
            // Espera SOLO DE PRUEBA con el bloqueo retenido (ver el XML doc de la clase).
            await Task.Delay(demoraCriticaMs);
            await session.CommitAsync();
            resultados.Add(resultado);
        }));
        cronometro.Stop();

        Assert.All(resultados, r => Assert.True(r.EsExito, r.EsFallo ? r.Errores[0].Codigo : string.Empty));
        var numeros = resultados.Select(r => r.Valor.Numero).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.Equal(Enumerable.Range(1, tareas).Select(n => prefijo + n.ToString().PadLeft(ancho, '0')), numeros);
        Assert.True(cronometro.ElapsedMilliseconds >= (tareas - 1) * demoraCriticaMs,
            $"Tiempo total ({cronometro.ElapsedMilliseconds} ms): las transacciones no se serializaron.");
    }

    [Fact]
    public async Task SiguienteAsync_SinTransaccionActiva_DevuelveFallo()
    {
        var (serieId, _) = await SembrarSerieAsync(new DateOnly(2026, 1, 1));
        await using var scope = _provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDbSession>().EnsureOpenAsync();

        var resultado = await Generador(scope).SiguienteAsync(serieId, TipoDocumentoSerie.DiarioInventario, new DateOnly(2026, 1, 1));

        Assert.Equal("numeracion.sin_transaccion", resultado.Errores[0].Codigo);
    }

    [Fact]
    public async Task SiguienteAsync_SerieInexistente_DevuelveFallo()
    {
        var resultado = await EnTransaccionAsync(g => g.SiguienteAsync(Guid.NewGuid(), TipoDocumentoSerie.DiarioInventario, new DateOnly(2026, 1, 1)));

        Assert.Equal(("numeracion.serie_inexistente", "SerieId"), (resultado.Errores[0].Codigo, resultado.Errores[0].Campo));
    }

    [Fact]
    public async Task SiguienteAsync_SinLineaVigenteParaLaFecha_DevuelveFallo()
    {
        var (serieId, _) = await SembrarSerieAsync(new DateOnly(2026, 6, 1));

        var resultado = await EnTransaccionAsync(g => g.SiguienteAsync(serieId, TipoDocumentoSerie.DiarioInventario, new DateOnly(2026, 1, 1)));

        Assert.Equal("numeracion.sin_linea_vigente", resultado.Errores[0].Codigo);
    }

    [Fact]
    public async Task SiguienteAsync_SerieAgotada_DevuelveFallo_SinTocarElContador()
    {
        var fecha = new DateOnly(2026, 1, 1);
        var (serieId, lineaId) = await SembrarSerieAsync(fecha, numeroInicial: "00001", numeroFinal: "00001", ultimoNumeroUsado: "00001");

        var resultado = await EnTransaccionConfirmadaAsync(g => g.SiguienteAsync(serieId, TipoDocumentoSerie.DiarioInventario, fecha));

        Assert.Equal("numeracion.serie_agotada", resultado.Errores[0].Codigo);
        Assert.Equal("00001", await UltimoAsync(lineaId));
    }

    [Fact]
    public async Task SiguienteAsync_SoloDigitos_SinCambios_YGuardaElNumeroCompleto()
    {
        var fecha = new DateOnly(2026, 1, 1);
        var (serieId, lineaId) = await SembrarSerieAsync(fecha, ultimoNumeroUsado: "00041");

        var resultado = await EnTransaccionAsync(g => g.SiguienteAsync(serieId, TipoDocumentoSerie.DiarioInventario, fecha), confirmar: true);

        Assert.Equal(new NumeroGenerado("00042", null), resultado.Valor);
        Assert.Equal("00042", await UltimoAsync(lineaId));
    }

    [Fact]
    public async Task SiguienteAsync_ContadorSinRelleno_SigueNumerandoIgual()
    {
        var fecha = new DateOnly(2026, 1, 1);
        var (serieId, lineaId) = await SembrarSerieAsync(fecha, numeroInicial: "00000001", numeroFinal: "99999999", ultimoNumeroUsado: "7");

        var resultado = await EnTransaccionAsync(g => g.SiguienteAsync(serieId, TipoDocumentoSerie.DiarioInventario, fecha), confirmar: true);

        Assert.Equal("00000008", resultado.Valor.Numero);
        Assert.Equal("00000008", await UltimoAsync(lineaId));
    }

    [Fact]
    public async Task SiguienteAsync_ConPrefijo_YLineaSinUsarConIncremento()
    {
        var fecha = new DateOnly(2026, 1, 1);
        var (serieId, lineaId) = await SembrarSerieAsync(fecha, numeroInicial: "A-0005", numeroFinal: "A-9999", ultimoNumeroUsado: "", incremento: 5);

        var primero = await EnTransaccionAsync(g => g.SiguienteAsync(serieId, TipoDocumentoSerie.DiarioInventario, fecha), confirmar: true);
        var segundo = await EnTransaccionAsync(g => g.SiguienteAsync(serieId, TipoDocumentoSerie.DiarioInventario, fecha), confirmar: true);

        Assert.Equal(("A-0005", "A-0010"), (primero.Valor.Numero, segundo.Valor.Numero));
        Assert.Equal("A-0010", await UltimoAsync(lineaId));
    }

    [Fact]
    public async Task SiguienteAsync_SerieInactiva_Falla_SinConsumir()
    {
        var fecha = new DateOnly(2026, 1, 1);
        var (serieId, lineaId) = await SembrarSerieAsync(fecha, activa: false);

        var resultado = await EnTransaccionConfirmadaAsync(g => g.SiguienteAsync(serieId, TipoDocumentoSerie.DiarioInventario, fecha));

        Assert.Equal(("numeracion.serie_inactiva", "SerieId"), (resultado.Errores[0].Codigo, resultado.Errores[0].Campo));
        Assert.Equal("00000", await UltimoAsync(lineaId));
    }

    [Fact]
    public async Task SiguienteAsync_TipoIncorrecto_Falla_SinConsumir()
    {
        var fecha = new DateOnly(2026, 1, 1);
        var (serieId, lineaId) = await SembrarSerieAsync(fecha, tipo: TipoDocumentoSerie.Cobro);

        var resultado = await EnTransaccionConfirmadaAsync(g => g.SiguienteAsync(serieId, TipoDocumentoSerie.FacturaVenta, fecha));

        Assert.Equal("numeracion.tipo_incorrecto", resultado.Errores[0].Codigo);
        Assert.Contains("Cobro de cliente", resultado.Errores[0].Mensaje);
        Assert.Equal("00000", await UltimoAsync(lineaId));
    }

    [Fact]
    public async Task SiguienteAsync_LasLineasMasRecientesBloqueadaYBorrada_UsaLaAnteriorVigente()
    {
        var (serieId, vigente) = await SembrarSerieAsync(new DateOnly(2026, 1, 1), ultimoNumeroUsado: "00010");
        var bloqueada = await AnadirLineaAsync(serieId, new DateOnly(2026, 3, 1), "00050", bloqueada: true);
        var borrada = await AnadirLineaAsync(serieId, new DateOnly(2026, 4, 1), "00070", borrada: true);

        var resultado = await EnTransaccionAsync(
            g => g.SiguienteAsync(serieId, TipoDocumentoSerie.DiarioInventario, new DateOnly(2026, 5, 1)), confirmar: true);

        Assert.Equal("00011", resultado.Valor.Numero);
        Assert.Equal(("00011", "00050", "00070"), (await UltimoAsync(vigente), await UltimoAsync(bloqueada), await UltimoAsync(borrada)));
    }

    [Theory]
    [InlineData(2026, 2, 28, "00011")]
    [InlineData(2026, 3, 1, "00051")]
    [InlineData(2026, 5, 31, "00051")]
    [InlineData(2026, 6, 1, "00091")]
    [InlineData(2027, 1, 1, "00091")]
    public async Task SiguienteAsync_VariasLineas_UsaLaUltimaConFechaInicialHastaLaFecha(int anio, int mes, int dia, string esperado)
    {
        var (serieId, primera) = await SembrarSerieAsync(new DateOnly(2026, 1, 1), ultimoNumeroUsado: "00010");
        var segunda = await AnadirLineaAsync(serieId, new DateOnly(2026, 3, 1), "00050");
        var tercera = await AnadirLineaAsync(serieId, new DateOnly(2026, 6, 1), "00090");

        var resultado = await EnTransaccionAsync(
            g => g.SiguienteAsync(serieId, TipoDocumentoSerie.DiarioInventario, new DateOnly(anio, mes, dia)), confirmar: true);

        Assert.Equal(esperado, resultado.Valor.Numero);
        var contadores = new[] { await UltimoAsync(primera), await UltimoAsync(segunda), await UltimoAsync(tercera) };
        Assert.Single(contadores, esperado);
    }

    [Fact]
    public async Task SiguientePorTipoAsync_UsaLaSerieConfigurada()
    {
        var fecha = new DateOnly(2026, 1, 1);
        var (serieId, _) = await SembrarSerieAsync(fecha, numeroInicial: "CFG-001", numeroFinal: "CFG-999", ultimoNumeroUsado: "");

        await using var scope = _provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDbSession>();
        await session.EnsureOpenAsync();
        await using var tx = await session.BeginTransactionAsync();
        // Dentro de la transacción (se deshace): la configuración del diario apunta a la serie de prueba.
        await session.Connection.ExecuteAsync(
            """UPDATE "ConfiguracionesNumeracion" SET "SerieId" = @S WHERE "TipoDocumento" = 8""", new { S = serieId }, session.CurrentTransaction);

        var resultado = await Generador(scope).SiguientePorTipoAsync(TipoDocumentoSerie.DiarioInventario, fecha);
        await session.RollbackAsync();

        Assert.Equal("CFG-001", resultado.Valor.Numero);
    }

    [Fact]
    public async Task SiguientePorTipoAsync_SinConfiguracion_Falla()
    {
        await using var scope = _provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDbSession>();
        await session.EnsureOpenAsync();
        await using var tx = await session.BeginTransactionAsync();
        await session.Connection.ExecuteAsync(
            """UPDATE "ConfiguracionesNumeracion" SET "IsDeleted" = true WHERE "TipoDocumento" = 5""", transaction: session.CurrentTransaction);

        var resultado = await Generador(scope).SiguientePorTipoAsync(TipoDocumentoSerie.Cobro, new DateOnly(2026, 1, 1));
        await session.RollbackAsync();

        Assert.Equal(("numeracion.sin_configuracion", "TipoDocumento"), (resultado.Errores[0].Codigo, resultado.Errores[0].Campo));
    }

    /// <summary>
    /// Final review 1: otra transacción reasigna la configuración del tipo a una serie nueva y desactiva la anterior mientras el
    /// posteo ya leyó la configuración (serie anterior) y espera en su <c>FOR SHARE</c>. Tras el commit ajeno, el generador vuelve a
    /// leer la configuración una vez y numera con la serie nueva en vez de fallar con <c>numeracion.serie_inactiva</c>.
    /// </summary>
    [Fact]
    public async Task SiguientePorTipoAsync_ConfiguracionReasignadaYSerieAnteriorDesactivadaEnVuelo_NumeraConLaNueva()
    {
        var fecha = new DateOnly(2026, 1, 1);
        var (anteriorId, anteriorLinea) = await SembrarSerieAsync(fecha, numeroInicial: "ANT-001", numeroFinal: "ANT-999", ultimoNumeroUsado: "");
        var (nuevaId, nuevaLinea) = await SembrarSerieAsync(fecha, numeroInicial: "NUE-001", numeroFinal: "NUE-999", ultimoNumeroUsado: "");
        await using var admin = new NpgsqlConnection(_fixture.AppConnectionString);
        await admin.OpenAsync();
        var original = await admin.ExecuteScalarAsync<Guid>(
            """SELECT "SerieId" FROM "ConfiguracionesNumeracion" WHERE "TipoDocumento" = 8 AND "IsDeleted" = false""");
        await admin.ExecuteAsync("""UPDATE "ConfiguracionesNumeracion" SET "SerieId" = @S WHERE "TipoDocumento" = 8""", new { S = anteriorId });
        try
        {
            // Transacción ajena sin confirmar: reasigna la configuración y desactiva la serie anterior (retiene sus filas).
            await using var ajena = new NpgsqlConnection(_fixture.AppConnectionString);
            await ajena.OpenAsync();
            await using var txAjena = await ajena.BeginTransactionAsync();
            await ajena.ExecuteAsync(
                """UPDATE "ConfiguracionesNumeracion" SET "SerieId" = @S WHERE "TipoDocumento" = 8""", new { S = nuevaId }, txAjena);
            await ajena.ExecuteAsync("""UPDATE "Series" SET "Activa" = false WHERE "Id" = @Id""", new { Id = anteriorId }, txAjena);

            await using var scope = _provider.CreateAsyncScope();
            var session = scope.ServiceProvider.GetRequiredService<IDbSession>();
            await session.EnsureOpenAsync();
            await using var tx = await session.BeginTransactionAsync();
            // Lee la configuración confirmada (serie anterior) y queda esperando en el FOR SHARE de la serie anterior.
            var numerando = Generador(scope).SiguientePorTipoAsync(TipoDocumentoSerie.DiarioInventario, fecha);
            await Task.Delay(500);
            Assert.False(numerando.IsCompleted, "El generador debía esperar al bloqueo de la serie anterior.");

            await txAjena.CommitAsync();
            var resultado = await numerando.WaitAsync(TimeSpan.FromSeconds(10));
            await session.CommitAsync();

            Assert.True(resultado.EsExito, resultado.EsFallo ? resultado.Errores[0].Codigo : string.Empty);
            Assert.Equal("NUE-001", resultado.Valor.Numero);
            Assert.Equal("NUE-001", await UltimoAsync(nuevaLinea));
            Assert.Equal(string.Empty, await UltimoAsync(anteriorLinea));
        }
        finally
        {
            await admin.ExecuteAsync("""UPDATE "ConfiguracionesNumeracion" SET "SerieId" = @S WHERE "TipoDocumento" = 8""", new { S = original });
        }
    }

    /// <summary>
    /// NS18 (residual): un posteo de factura numera y retiene la serie <c>FOR SHARE</c> hasta su commit. La desactivación concurrente
    /// (administración de series, que bloquea la serie <c>FOR UPDATE</c>) espera al posteo y después se aplica; el posteo numeró con
    /// la serie aún válida y el contador guarda exactamente ese número (ninguno perdido ni repetido). Después la serie ya no numera.
    /// </summary>
    [Fact]
    public async Task NS18_DesactivacionConcurrente_EsperaAlPosteo_YDespuesSeAplica_SinPerderNumeros()
    {
        var fecha = new DateOnly(2026, 1, 1);
        var p = Prefijo();
        var (serieId, lineaId) = await SembrarSerieAsync(fecha, tipo: TipoDocumentoSerie.FacturaVenta, numeroInicial: $"{p}001", numeroFinal: $"{p}999", ultimoNumeroUsado: "");

        var (numero, admin) = await PosteoConAdministracionConcurrenteAsync(serieId, fecha, TipoDocumentoSerie.FacturaVenta, activa: false, confirmarPosteo: true);

        Assert.True(admin.EsExito, admin.EsFallo ? admin.Errores[0].Codigo : string.Empty);
        Assert.False(admin.Valor.Activa);
        Assert.Equal($"{p}001", numero.Valor.Numero);
        Assert.Equal($"{p}001", await UltimoAsync(lineaId));
        var despues = await EnTransaccionAsync(g => g.SiguienteAsync(serieId, TipoDocumentoSerie.FacturaVenta, fecha));
        Assert.Equal("numeracion.serie_inactiva", despues.Errores[0].Codigo);
        Assert.Equal($"{p}001", await UltimoAsync(lineaId));
    }

    /// <summary>
    /// NS18 (residual), cambio de tipo: espera igual al posteo. Si el posteo se confirma, la serie ya emitió números y el cambio se
    /// rechaza (409); si el posteo se deshace, el cambio se aplica y el contador vuelve a quedar sin usar (ningún número perdido).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NS18_CambioDeTipoConcurrente_EsperaAlPosteo_YSeDecideConLoQueQuedo(bool confirmarPosteo)
    {
        var fecha = new DateOnly(2026, 1, 1);
        var p = Prefijo();
        var (serieId, lineaId) = await SembrarSerieAsync(fecha, tipo: TipoDocumentoSerie.FacturaVenta, numeroInicial: $"{p}001", numeroFinal: $"{p}999", ultimoNumeroUsado: "");

        var (numero, admin) = await PosteoConAdministracionConcurrenteAsync(serieId, fecha, TipoDocumentoSerie.Cobro, activa: true, confirmarPosteo);

        Assert.Equal($"{p}001", numero.Valor.Numero);
        if (confirmarPosteo)
        {
            Assert.Equal(("serie.tipo_en_uso.conflicto", "TipoDocumento"), (admin.Errores[0].Codigo, admin.Errores[0].Campo));
            Assert.Equal($"{p}001", await UltimoAsync(lineaId));
            var siguiente = await EnTransaccionAsync(g => g.SiguienteAsync(serieId, TipoDocumentoSerie.FacturaVenta, fecha));
            Assert.Equal($"{p}002", siguiente.Valor.Numero);
        }
        else
        {
            Assert.True(admin.EsExito, admin.EsFallo ? admin.Errores[0].Codigo : string.Empty);
            Assert.Equal(TipoDocumentoSerie.Cobro, admin.Valor.TipoDocumento);
            Assert.Equal(string.Empty, await UltimoAsync(lineaId));
            var cobro = await EnTransaccionAsync(g => g.SiguienteAsync(serieId, TipoDocumentoSerie.Cobro, fecha));
            Assert.Equal($"{p}001", cobro.Valor.Numero);
        }
    }

    /// <summary>
    /// "Posteo": transacción que numera con la serie (FOR SHARE de la serie, FOR UPDATE de su línea) y no termina todavía. Mientras,
    /// la administración de series (handler real) modifica la cabecera; debe quedar bloqueada por el posteo. Luego el posteo se
    /// confirma o se deshace y se espera a la administración.
    /// </summary>
    private async Task<(Result<NumeroGenerado> Numero, Result<SerieResponse> Admin)> PosteoConAdministracionConcurrenteAsync(
        Guid serieId, DateOnly fecha, TipoDocumentoSerie tipoNuevo, bool activa, bool confirmarPosteo)
    {
        string codigo;
        long xmin;
        await using (var conexion = new NpgsqlConnection(_fixture.AppConnectionString))
        {
            (codigo, xmin) = await conexion.QuerySingleAsync<(string, long)>(
                """SELECT "Codigo", xmin::text::bigint FROM "Series" WHERE "Id" = @Id""", new { Id = serieId });
        }

        await using var posteo = _provider.CreateAsyncScope();
        var session = posteo.ServiceProvider.GetRequiredService<IDbSession>();
        await session.EnsureOpenAsync();
        await using var tx = await session.BeginTransactionAsync();
        var numero = await Generador(posteo).SiguienteAsync(serieId, TipoDocumentoSerie.FacturaVenta, fecha);
        Assert.True(numero.EsExito, numero.EsFallo ? numero.Errores[0].Codigo : string.Empty);

        var administracion = Task.Run(async () =>
        {
            await using var scope = _provider.CreateAsyncScope();
            var handler = ActivatorUtilities.CreateInstance<UpdateSerieCommandHandler>(scope.ServiceProvider);
            return await handler.Handle(new UpdateSerieCommand(serieId, codigo, "Serie de prueba NS18", tipoNuevo, false, activa, xmin), default);
        });
        await EsperaBloqueo.EsperarBloqueadaPorAsync(
            _fixture.AppConnectionString, ((NpgsqlConnection)session.Connection).ProcessID, administracion,
            "La administración de la serie debía esperar al posteo");

        if (confirmarPosteo)
        {
            await session.CommitAsync();
        }
        else
        {
            await session.RollbackAsync();
        }

        return (numero, await administracion.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    private static string Prefijo() => $"N{Guid.NewGuid():N}"[..5].ToUpperInvariant() + "-";

    [Fact]
    public async Task SiguientePorTipoAsync_SerieConfiguradaInactivaSinReasignar_SigueFallando()
    {
        var fecha = new DateOnly(2026, 1, 1);
        var (serieId, lineaId) = await SembrarSerieAsync(fecha, activa: false, ultimoNumeroUsado: "00003");

        await using var scope = _provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDbSession>();
        await session.EnsureOpenAsync();
        await using var tx = await session.BeginTransactionAsync();
        await session.Connection.ExecuteAsync(
            """UPDATE "ConfiguracionesNumeracion" SET "SerieId" = @S WHERE "TipoDocumento" = 8""", new { S = serieId }, session.CurrentTransaction);

        var resultado = await Generador(scope).SiguientePorTipoAsync(TipoDocumentoSerie.DiarioInventario, fecha);
        await session.RollbackAsync();

        Assert.Equal(("numeracion.serie_inactiva", "SerieId"), (resultado.Errores[0].Codigo, resultado.Errores[0].Campo));
        Assert.Equal("00003", await UltimoAsync(lineaId));
    }

    [Fact]
    public async Task SiguienteAsync_AlAlcanzarElNumeroDeAviso_DevuelveLaAdvertencia()
    {
        var fecha = new DateOnly(2026, 1, 1);
        var (serieId, _) = await SembrarSerieAsync(fecha, ultimoNumeroUsado: "00004", numeroAviso: "00005");

        var resultado = await EnTransaccionAsync(g => g.SiguienteAsync(serieId, TipoDocumentoSerie.DiarioInventario, fecha), confirmar: true);

        Assert.Equal("00005", resultado.Valor.Numero);
        Assert.Contains("número de aviso", resultado.Valor.Aviso);
    }

    [Fact]
    public async Task ProximoNumeroAsync_NoConsume_NoExigeTransaccion_NiEsperaAlBloqueo()
    {
        var fecha = new DateOnly(2026, 1, 1);
        var (serieId, lineaId) = await SembrarSerieAsync(fecha, ultimoNumeroUsado: "00009");

        // Otra transacción retiene el FOR UPDATE de la línea (numera y no confirma todavía).
        await using var bloqueo = _provider.CreateAsyncScope();
        var sesionBloqueo = bloqueo.ServiceProvider.GetRequiredService<IDbSession>();
        await sesionBloqueo.EnsureOpenAsync();
        await using var tx = await sesionBloqueo.BeginTransactionAsync();
        Assert.True((await Generador(bloqueo).SiguienteAsync(serieId, TipoDocumentoSerie.DiarioInventario, fecha)).EsExito);

        await using var lectura = _provider.CreateAsyncScope();
        var proximo = await Generador(lectura).ProximoNumeroAsync(serieId, fecha).WaitAsync(TimeSpan.FromSeconds(5));

        await sesionBloqueo.RollbackAsync();
        Assert.Equal("00010", proximo.Valor.Numero);
        Assert.Equal("00009", await UltimoAsync(lineaId));
    }

    [Fact]
    public async Task ProximoNumeroAsync_ConTipoEsperado_ValidaElTipo_SinTipoNoLoValida()
    {
        var fecha = new DateOnly(2026, 1, 1);
        var (serieId, _) = await SembrarSerieAsync(fecha, tipo: TipoDocumentoSerie.Cobro, ultimoNumeroUsado: "00004");
        await using var scope = _provider.CreateAsyncScope();
        var generador = Generador(scope);

        var incorrecto = await generador.ProximoNumeroAsync(serieId, fecha, TipoDocumentoSerie.FacturaVenta);
        var correcto = await generador.ProximoNumeroAsync(serieId, fecha, TipoDocumentoSerie.Cobro);
        var sinTipo = await generador.ProximoNumeroAsync(serieId, fecha);

        Assert.Equal(("numeracion.tipo_incorrecto", "SerieId"), (incorrecto.Errores[0].Codigo, incorrecto.Errores[0].Campo));
        Assert.Equal("00005", correcto.Valor.Numero);
        Assert.Equal("00005", sinTipo.Valor.Numero);
    }

    [Fact]
    public async Task ValidarSerieAsync_YSerieConfiguradaAsync()
    {
        var (serieId, _) = await SembrarSerieAsync(new DateOnly(2026, 1, 1), tipo: TipoDocumentoSerie.Cobro);
        await using var scope = _provider.CreateAsyncScope();
        var generador = Generador(scope);

        Assert.True((await generador.ValidarSerieAsync(serieId, TipoDocumentoSerie.Cobro)).EsExito);
        Assert.Equal("numeracion.tipo_incorrecto", (await generador.ValidarSerieAsync(serieId, TipoDocumentoSerie.FacturaVenta)).Errores[0].Codigo);
        Assert.Equal(SerieCobroIds.SerieId, (await generador.SerieConfiguradaAsync(TipoDocumentoSerie.Cobro)).Valor);
    }

    private static IGeneradorNumeroDocumento Generador(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IGeneradorNumeroDocumento>();

    private async Task<Result<T>> EnTransaccionAsync<T>(Func<IGeneradorNumeroDocumento, Task<Result<T>>> accion, bool confirmar = false)
    {
        await using var scope = _provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDbSession>();
        await session.EnsureOpenAsync();
        await using var tx = await session.BeginTransactionAsync();
        var resultado = await accion(Generador(scope));
        if (confirmar && resultado.EsExito)
        {
            await session.CommitAsync();
        }
        else
        {
            await session.RollbackAsync();
        }

        return resultado;
    }

    /// <summary>
    /// Confirma SIEMPRE (también si el generador falla) y devuelve el control con la transacción ya cerrada: así un contador
    /// leído después desde otra conexión (<see cref="UltimoAsync"/>) demuestra que el fallo no escribió nada; con rollback, una
    /// escritura previa al fallo quedaría oculta y la aserción no podría fallar.
    /// </summary>
    private async Task<Result<T>> EnTransaccionConfirmadaAsync<T>(Func<IGeneradorNumeroDocumento, Task<Result<T>>> accion)
    {
        await using var scope = _provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDbSession>();
        await session.EnsureOpenAsync();
        await using var tx = await session.BeginTransactionAsync();
        var resultado = await accion(Generador(scope));
        await session.CommitAsync();
        return resultado;
    }

    private async Task<Guid> AnadirLineaAsync(
        Guid serieId, DateOnly fechaInicial, string ultimoNumeroUsado, bool bloqueada = false, bool borrada = false)
    {
        var id = Guid.NewGuid();
        await using var conexion = new NpgsqlConnection(_fixture.AppConnectionString);
        await conexion.ExecuteAsync(
            """
            INSERT INTO "LineasSerie" ("Id", "SerieId", "NumeroInicial", "NumeroFinal", "UltimoNumeroUsado", "FechaInicial",
                                       "Incremento", "Bloqueada", "CreatedAtUtc", "CreatedBy", "IsDeleted")
            VALUES (@Id, @SerieId, '00001', '00100', @Ultimo, @Fecha, 1, @Bloqueada, now(), 'test', @Borrada)
            """,
            new { Id = id, SerieId = serieId, Ultimo = ultimoNumeroUsado, Fecha = fechaInicial.ToDateTime(TimeOnly.MinValue), Bloqueada = bloqueada, Borrada = borrada });
        return id;
    }

    private async Task<string> UltimoAsync(Guid lineaId)
    {
        await using var conexion = new NpgsqlConnection(_fixture.AppConnectionString);
        return (await conexion.ExecuteScalarAsync<string>(
            """SELECT "UltimoNumeroUsado" FROM "LineasSerie" WHERE "Id" = @Id""", new { Id = lineaId }))!;
    }

    private async Task<(Guid SerieId, Guid LineaId)> SembrarSerieAsync(
        DateOnly fechaInicial,
        TipoDocumentoSerie tipo = TipoDocumentoSerie.DiarioInventario,
        bool activa = true,
        string numeroInicial = "00001",
        string numeroFinal = "00100",
        string ultimoNumeroUsado = "00000",
        int incremento = 1,
        string? numeroAviso = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_fixture.AppConnectionString).Options;
        await using var context = new ApplicationDbContext(options);
        var serie = new Serie
        {
            Codigo = $"GEN{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Descripcion = "Serie de prueba de numeración",
            TipoDocumento = tipo,
            Activa = activa,
        };
        context.Series.Add(serie);
        var linea = new LineaSerie
        {
            SerieId = serie.Id,
            NumeroInicial = numeroInicial,
            NumeroFinal = numeroFinal,
            NumeroAviso = numeroAviso,
            UltimoNumeroUsado = ultimoNumeroUsado,
            FechaInicial = fechaInicial,
            Incremento = incremento,
        };
        context.LineasSerie.Add(linea);
        await context.SaveChangesAsync();
        return (serie.Id, linea.Id);
    }
}
