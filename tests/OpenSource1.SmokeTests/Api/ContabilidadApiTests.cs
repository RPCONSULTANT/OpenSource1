using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenSource1.Api;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.Contabilidad.Dtos;
using OpenSource1.Application.Features.CuentasContables.Dtos;
using OpenSource1.Application.Features.GruposContables.Dtos;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

/// <summary>
/// API del libro contable (Task 5.5): <c>GET api/contabilidad/movimientos</c> (filtros por cuenta y fechas, paginado, orden
/// estable) y <c>GET api/contabilidad/registros</c> (con totales derivados), ambos CanConsult; y la guarda de cuentas ampliada:
/// una cuenta con movimientos contables está en uso (borrarla o cambiar su TipoCuenta de Posteo → 409). Los asientos se
/// escriben con el <see cref="IRegistroContable"/> real del host. REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ContabilidadApiTests : IClassFixture<PostgresTestFixture>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ContabilidadApiTests(PostgresTestFixture fixture)
    {
        _factory = fixture.CreateFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Consultas_ExigenAutenticacion_YSupervisorPuedeConsultar()
    {
        // Cliente propio, sin cabeceras por defecto: las de cada petición no se mezclan con las de Admin().
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        foreach (var url in new[] { "/api/contabilidad/movimientos", "/api/contabilidad/registros" })
        {
            var anon = new HttpRequestMessage(HttpMethod.Get, url);
            anon.Headers.Add("X-Test-Anonymous", "true");
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(anon)).StatusCode);

            var supervisor = new HttpRequestMessage(HttpMethod.Get, url);
            supervisor.Headers.Add("X-Test-User", "supervisor");
            supervisor.Headers.Add("X-Test-Roles", "Supervisor");
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(supervisor)).StatusCode);
        }
    }

    [Fact]
    public async Task Movimientos_FiltraPorCuentaYFechas_PaginaYOrdenaCronologicamente()
    {
        var a = await CrearCuentaAsync();
        var b = await CrearCuentaAsync();
        var r1 = await RegistrarAsync(new DateOnly(2026, 3, 10), (a.Id, 10m), (b.Id, -10m));
        var r2 = await RegistrarAsync(new DateOnly(2026, 3, 5), (a.Id, -4m), (b.Id, 4m));
        await RegistrarAsync(new DateOnly(2026, 4, 1), (a.Id, 1m), (b.Id, -1m));

        var marzo = await GetAsync<PagedResult<MovimientoContableResponse>>(
            $"/api/contabilidad/movimientos?cuentaId={a.Id}&desde=2026-03-01&hasta=2026-03-31");
        Assert.Equal(2, marzo.Total);
        // Orden cronológico por defecto: el del 5 de marzo (registrado después) va primero.
        Assert.Equal([new DateOnly(2026, 3, 5), new DateOnly(2026, 3, 10)], marzo.Items.Select(x => x.FechaRegistro));
        Assert.Equal([-4m, 10m], marzo.Items.Select(x => x.Importe));
        Assert.Equal((4m, 0m), (marzo.Items[0].Credito, marzo.Items[0].Debito));
        Assert.All(marzo.Items, x => Assert.Equal(a.Numero, x.NumeroCuenta));
        Assert.All(marzo.Items, x => Assert.Equal(a.Nombre, x.NombreCuenta));
        Assert.Equal(r2.NumeroRegistro, marzo.Items[0].NumeroRegistro);
        Assert.Equal(r1.RegistroContableId, marzo.Items[1].RegistroContableId);

        var pagina2 = await GetAsync<PagedResult<MovimientoContableResponse>>(
            $"/api/contabilidad/movimientos?cuentaId={a.Id}&tamanoPagina=1&pagina=2");
        Assert.Equal(3, pagina2.Total);
        Assert.Equal(new DateOnly(2026, 3, 10), Assert.Single(pagina2.Items).FechaRegistro);

        var porImporteDesc = await GetAsync<PagedResult<MovimientoContableResponse>>(
            $"/api/contabilidad/movimientos?cuentaId={a.Id}&ordenarPor=Importe&descendente=true");
        Assert.Equal([10m, 1m, -4m], porImporteDesc.Items.Select(x => x.Importe));

        // Columna fuera de la allow-list: se ignora (orden por defecto), nunca se interpola.
        var inyeccion = await GetAsync<PagedResult<MovimientoContableResponse>>(
            $"/api/contabilidad/movimientos?cuentaId={a.Id}&ordenarPor={Uri.EscapeDataString("\"Importe\"; DROP TABLE x")}");
        Assert.Equal(3, inyeccion.Total);
    }

    [Fact]
    public async Task Movimientos_RangoDeFechasInvertido_Devuelve400()
    {
        var respuesta = await Admin().GetAsync("/api/contabilidad/movimientos?desde=2026-05-01&hasta=2026-04-01");
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        // El cuerpo es un ValidationProblemDetails agrupado por campo (el código no viaja en el cuerpo).
        Assert.Contains("posterior a 'hasta'", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Registros_MasRecientePrimero_ConTotalesDerivados()
    {
        var a = await CrearCuentaAsync();
        var b = await CrearCuentaAsync();
        var r1 = await RegistrarAsync(new DateOnly(2026, 5, 1), (a.Id, 7.5m), (b.Id, -2.5m), (b.Id, -5m));
        var r2 = await RegistrarAsync(new DateOnly(2026, 5, 2), (a.Id, -1m), (b.Id, 1m));

        var lista = await GetAsync<PagedResult<RegistroContableResponse>>("/api/contabilidad/registros?tamanoPagina=2");

        Assert.Equal([r2.RegistroContableId, r1.RegistroContableId], lista.Items.Select(x => x.Id));
        var primero = lista.Items[1];
        Assert.Equal(r1.NumeroRegistro, primero.NumeroRegistro);
        Assert.Equal((r1.DesdeMovimiento, r1.HastaMovimiento), (primero.DesdeMovimiento, primero.HastaMovimiento));
        Assert.Equal((3, 7.5m, 7.5m), (primero.Movimientos, primero.TotalDebito, primero.TotalCredito));
        Assert.Equal(TipoOrigenMovimiento.Diario, primero.TipoOrigen);

        var ascendente = await GetAsync<PagedResult<RegistroContableResponse>>(
            "/api/contabilidad/registros?descendente=false&ordenarPor=NumeroRegistro&tamanoPagina=200");
        Assert.Equal(ascendente.Items.Select(x => x.NumeroRegistro).Order(StringComparer.Ordinal), ascendente.Items.Select(x => x.NumeroRegistro));
    }

    [Fact]
    public async Task CuentaConMovimientosContables_BorrarOCambiarTipoCuenta_Devuelve409()
    {
        var a = await CrearCuentaAsync();
        var b = await CrearCuentaAsync();
        await RegistrarAsync(new DateOnly(2026, 6, 1), (a.Id, 3m), (b.Id, -3m));
        var client = Admin();

        var delete = await client.DeleteAsync($"/api/cuentas-contables/{a.Id}");
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);

        var actual = await GetAsync<CuentaContableResponse>($"/api/cuentas-contables/{a.Id}");
        var update = await client.PutAsJsonAsync(
            $"/api/cuentas-contables/{a.Id}",
            new { numero = a.Numero, nombre = a.Nombre, tipoCuenta = TipoCuentaContable.Encabezado, tipoResultado = TipoResultadoCuenta.Balance, posteoDirecto = (bool?)null, bloqueada = (bool?)null, sangria = (int?)null, xmin = actual.Xmin });
        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);

        // Seguir siendo de Posteo (p. ej. renombrarla) sí se permite.
        var renombrar = await client.PutAsJsonAsync(
            $"/api/cuentas-contables/{a.Id}",
            new { numero = a.Numero, nombre = "Renombrada", tipoCuenta = TipoCuentaContable.Posteo, tipoResultado = TipoResultadoCuenta.Balance, posteoDirecto = (bool?)null, bloqueada = (bool?)null, sangria = (int?)null, xmin = actual.Xmin });
        Assert.Equal(HttpStatusCode.OK, renombrar.StatusCode);
    }

    /// <summary>
    /// Carrera borrado de cuenta / registro contable (ronda 1): con un <c>RegistrarAsync</c> sin confirmar sobre la cuenta, el
    /// DELETE espera en el <c>FOR UPDATE</c> de la cuenta (el registro la tiene <c>FOR SHARE</c>) y, tras el commit, ve los
    /// movimientos → 409. Sin el bloqueo, la guarda no ve los movimientos aún no confirmados y el borrado lógico acabaría en 204.
    /// </summary>
    [Fact]
    public async Task BorrarCuenta_MientrasUnRegistroContableEnCursoLaUsa_EsperaYDevuelve409()
    {
        var a = await CrearCuentaAsync();
        var b = await CrearCuentaAsync();
        var client = Admin();

        await using var scope = _factory.Services.CreateAsyncScope();
        var sesion = scope.ServiceProvider.GetRequiredService<IDbSession>();
        var registro = scope.ServiceProvider.GetRequiredService<IRegistroContable>();
        await using (await sesion.BeginTransactionAsync())
        {
            var resultado = await registro.RegistrarAsync(new AsientoContable(
                new DateOnly(2026, 6, 2), new DateOnly(2026, 6, 2), TipoDocumentoContable.Ninguno, null, "En curso",
                TipoOrigenMovimiento.Diario, $"API-{Guid.NewGuid():N}"[..30],
                [new LineaAsiento(a.Id, 2m, null), new LineaAsiento(b.Id, -2m, null)]));
            Assert.True(resultado.EsExito);

            var borrado = client.DeleteAsync($"/api/cuentas-contables/{a.Id}");
            await Task.Delay(TimeSpan.FromSeconds(1.5));
            Assert.False(borrado.IsCompleted, "El borrado no debía avanzar mientras el registro contable siga sin confirmar.");

            await sesion.CommitAsync();
            Assert.Equal(HttpStatusCode.Conflict, (await borrado).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/cuentas-contables/{a.Id}")).StatusCode);
    }

    [Fact]
    public async Task BorrarGrupoCongeladoEnUnMovimientoDeValor_Devuelve409()
    {
        var client = Admin();
        var grupo = await CrearGrupoAsync("inventario");

        // Producto con ese grupo de inventario, un movimiento de valor que lo congela y luego el producto pasa a GENERAL: ya
        // ningún maestro usa el grupo, solo el libro.
        Guid productoId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var contexto = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nuevo = new Producto
            {
                Codigo = $"LC{Guid.NewGuid():N}"[..20],
                Nombre = "Producto del libro contable",
                PrecioVenta = 1m,
                CategoriaId = Guid.Parse("c1000000-0000-0000-0000-000000000001"),
                UnidadMedidaBaseId = LibroInventarioPrueba.UnidadUnd,
                GrupoInventarioId = grupo,
                CreatedBy = "test",
            };
            contexto.Productos.Add(nuevo);
            await contexto.SaveChangesAsync();
            productoId = nuevo.Id;

            var sesion = scope.ServiceProvider.GetRequiredService<IDbSession>();
            await using (await sesion.BeginTransactionAsync())
            {
                var registrado = await scope.ServiceProvider.GetRequiredService<IRegistroMovimientosInventario>().RegistrarAsync(
                    LibroInventarioPrueba.Entrada(productoId, AlmacenIds.Principal, 1m, 1m, new DateOnly(2026, 6, 3)));
                Assert.True(registrado.EsExito);
                await sesion.CommitAsync();
            }
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var contexto = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var producto = await contexto.Productos.SingleAsync(x => x.Id == productoId);
            producto.GrupoInventarioId = GrupoContableIds.InventarioGeneral;
            await contexto.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/grupos-contables/inventario/{grupo}")).StatusCode);
    }

    [Fact]
    public async Task BorrarGrupoCongeladoEnUnMovimientoContable_Devuelve409()
    {
        var client = Admin();
        var a = await CrearCuentaAsync();
        var b = await CrearCuentaAsync();
        var grupo = await CrearGrupoAsync("iva-negocio");
        var libre = await CrearGrupoAsync("iva-negocio");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sesion = scope.ServiceProvider.GetRequiredService<IDbSession>();
            await using (await sesion.BeginTransactionAsync())
            {
                var resultado = await scope.ServiceProvider.GetRequiredService<IRegistroContable>().RegistrarAsync(new AsientoContable(
                    new DateOnly(2026, 6, 4), new DateOnly(2026, 6, 4), TipoDocumentoContable.Ninguno, null, "Con grupo",
                    TipoOrigenMovimiento.Diario, $"API-{Guid.NewGuid():N}"[..30],
                    [new LineaAsiento(a.Id, 1m, null, GrupoIvaNegocioId: grupo), new LineaAsiento(b.Id, -1m, null)]));
                Assert.True(resultado.EsExito);
                await sesion.CommitAsync();
            }
        }

        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/grupos-contables/iva-negocio/{grupo}")).StatusCode);
        // Control: un grupo del mismo tipo sin uso sí se borra.
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/grupos-contables/iva-negocio/{libre}")).StatusCode);
    }

    // ── Helpers ──

    [Fact]
    public async Task PostearCostoInventario_ExigeCanModify_YDevuelveElResumenConPendientes()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        const string url = "/api/contabilidad/postear-costo-inventario";

        var anon = new HttpRequestMessage(HttpMethod.Post, url);
        anon.Headers.Add("X-Test-Anonymous", "true");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(anon)).StatusCode);

        // Ejecutor (consultar + agregar) no tiene CanModify: 403 sin ejecutar nada.
        var ejecutor = new HttpRequestMessage(HttpMethod.Post, url);
        ejecutor.Headers.Add("X-Test-User", "ejecutor");
        ejecutor.Headers.Add("X-Test-Roles", "Ejecutor");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(ejecutor)).StatusCode);

        // Supervisor sí tiene CanModify: 200 con el resumen.
        var supervisor = new HttpRequestMessage(HttpMethod.Post, url);
        supervisor.Headers.Add("X-Test-User", "supervisor");
        supervisor.Headers.Add("X-Test-Roles", "Supervisor");
        var respuestaSupervisor = await client.SendAsync(supervisor);
        Assert.Equal(HttpStatusCode.OK, respuestaSupervisor.StatusCode);
        Assert.NotNull(await respuestaSupervisor.Content.ReadFromJsonAsync<ResultadoPosteoCostoInventario>());

        // Un producto clasificado (se contabiliza) y otro sin grupos (queda pendiente con grupo_faltante).
        var clasificado = await ProductoConEntradaAsync(GrupoContableIds.InventarioGeneral);
        var sinGrupos = await ProductoConEntradaAsync(null);

        var respuesta = await Admin().PostAsync(url, content: null);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("asientos").GetInt32() >= 1);
        Assert.True(json.RootElement.GetProperty("movimientosValorContabilizados").GetInt32() >= 1);
        var pendiente = Assert.Single(
            json.RootElement.GetProperty("pendientes").EnumerateArray(),
            p => p.GetProperty("movimientoValorId").GetInt64() == sinGrupos.MovimientoValorId);
        Assert.Equal("setup_contable.grupo_faltante", pendiente.GetProperty("codigo").GetString());
        Assert.False(string.IsNullOrWhiteSpace(pendiente.GetProperty("mensaje").GetString()));
        Assert.DoesNotContain(
            json.RootElement.GetProperty("pendientes").EnumerateArray(),
            p => p.GetProperty("movimientoValorId").GetInt64() == clasificado.MovimientoValorId);

        // Idempotente: la segunda ejecución no crea asientos; filtrada por producto no ve el pendiente del otro.
        var segunda = await Admin().PostAsync($"{url}?productoId={clasificado.ProductoId}", content: null);
        var cuerpo = await segunda.Content.ReadFromJsonAsync<ResultadoPosteoCostoInventario>();
        Assert.Equal(new ResultadoPosteoCostoInventario(0, 0, []), cuerpo! with { Pendientes = [] });
        Assert.Empty(cuerpo.Pendientes);
    }

    private async Task<(Guid ProductoId, long MovimientoValorId)> ProductoConEntradaAsync(Guid? grupoInventario)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var contexto = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var producto = new Producto
        {
            Codigo = $"PC{Guid.NewGuid():N}"[..20],
            Nombre = "Producto del batch de costo",
            PrecioVenta = 1m,
            CategoriaId = Guid.Parse("c1000000-0000-0000-0000-000000000001"),
            UnidadMedidaBaseId = LibroInventarioPrueba.UnidadUnd,
            GrupoInventarioId = grupoInventario,
            GrupoProductoId = grupoInventario is null ? null : GrupoContableIds.ProductoBienes,
            CreatedBy = "test",
        };
        contexto.Productos.Add(producto);
        await contexto.SaveChangesAsync();

        var sesion = scope.ServiceProvider.GetRequiredService<IDbSession>();
        await using (await sesion.BeginTransactionAsync())
        {
            var registrado = await scope.ServiceProvider.GetRequiredService<IRegistroMovimientosInventario>().RegistrarAsync(
                LibroInventarioPrueba.Entrada(producto.Id, AlmacenIds.Principal, 2m, 5m, new DateOnly(2026, 6, 10)));
            Assert.True(registrado.EsExito);
            await sesion.CommitAsync();
            return (producto.Id, registrado.Valor.MovimientoValorId);
        }
    }

    private async Task<AsientoRegistrado> RegistrarAsync(DateOnly fecha, params (Guid Cuenta, decimal Importe)[] lineas)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var sesion = scope.ServiceProvider.GetRequiredService<IDbSession>();
        var registro = scope.ServiceProvider.GetRequiredService<IRegistroContable>();

        await using var tx = await sesion.BeginTransactionAsync();
        var resultado = await registro.RegistrarAsync(new AsientoContable(
            fecha, fecha, TipoDocumentoContable.Ninguno, null, "Asiento de la API", TipoOrigenMovimiento.Diario,
            $"API-{Guid.NewGuid():N}"[..30], [.. lineas.Select(x => new LineaAsiento(x.Cuenta, x.Importe, null))]));
        Assert.True(resultado.EsExito, resultado.EsFallo ? string.Join("; ", resultado.Errores.Select(e => e.Mensaje)) : "");
        await sesion.CommitAsync();
        return resultado.Valor;
    }

    private async Task<Guid> CrearGrupoAsync(string ruta)
    {
        var respuesta = await Admin().PostAsJsonAsync(
            $"/api/grupos-contables/{ruta}", new { codigo = $"G{Guid.NewGuid():N}"[..20].ToUpperInvariant(), descripcion = "Grupo del libro" });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<GrupoContableResponse>())!.Id;
    }

    private async Task<CuentaContableResponse> CrearCuentaAsync()
    {
        var respuesta = await Admin().PostAsJsonAsync(
            "/api/cuentas-contables",
            new { numero = NuevoNumero(), nombre = $"Libro {Guid.NewGuid():N}"[..30], tipoCuenta = TipoCuentaContable.Posteo, tipoResultado = TipoResultadoCuenta.Balance, posteoDirecto = true, bloqueada = false, sangria = 1 });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<CuentaContableResponse>())!;
    }

    private async Task<T> GetAsync<T>(string url)
    {
        var respuesta = await Admin().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<T>())!;
    }

    private HttpClient Admin()
    {
        _client.DefaultRequestHeaders.Remove("X-Test-User");
        _client.DefaultRequestHeaders.Remove("X-Test-Roles");
        _client.DefaultRequestHeaders.Add("X-Test-User", "administrador");
        _client.DefaultRequestHeaders.Add("X-Test-Roles", "Administrador");
        return _client;
    }

    private static long _contador = DateTime.UtcNow.Ticks % 1_000_000;

    private static string NuevoNumero() => $"7{Interlocked.Increment(ref _contador)}";
}
