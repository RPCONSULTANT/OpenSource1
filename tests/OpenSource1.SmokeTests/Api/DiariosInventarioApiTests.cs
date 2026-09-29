using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenSource1.Application.Data;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Plantillas.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Registros.Commands;
using OpenSource1.Application.Features.DiariosInventario.Registros.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Inventario;
using OpenSource1.Core.Enums;
using OpenSource1.Infrastructure.Data;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

[Collection(PostgresCollection.Name)]
public sealed class DiariosInventarioApiTests : IClassFixture<PostgresTestFixture>
{
    private static readonly Guid UnidadUnd = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly PostgresTestFixture _fixture;
    private readonly HttpClient _client;

    public DiariosInventarioApiTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
        var factory = fixture.CreateFactory();
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    // ----- Plantillas -----

    [Fact]
    public async Task ListPlantillas_DevuelveLasDosSembradasConLaMismaSerie()
    {
        var client = CreateClient("Administrador");

        var response = await client.GetAsync("/api/diarios-inventario/plantillas");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var plantillas = await response.Content.ReadFromJsonAsync<List<PlantillaDiarioResponse>>();
        Assert.NotNull(plantillas);
        Assert.Equal(2, plantillas!.Count);

        var articulo = Assert.Single(plantillas, p => p.Codigo == "ARTICULO");
        Assert.Equal(TipoPlantillaDiario.Articulo, articulo.Tipo);
        var reclasif = Assert.Single(plantillas, p => p.Codigo == "RECLASIF");
        Assert.Equal(TipoPlantillaDiario.Reclasificacion, reclasif.Tipo);
        Assert.Equal(articulo.SerieId, reclasif.SerieId);

        // Sin CRUD (sembradas, de solo lectura): no hay más rutas bajo /plantillas que el listado. Cualquier rol con
        // CanConsult puede leerlas.
        Assert.Equal(HttpStatusCode.OK, (await CreateClient("Supervisor").GetAsync("/api/diarios-inventario/plantillas")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await CreateClient("Ejecutor").GetAsync("/api/diarios-inventario/plantillas")).StatusCode);
    }

    [Fact]
    public async Task ListPlantillas_AnonimoDevuelve401()
    {
        var anon = new HttpRequestMessage(HttpMethod.Get, "/api/diarios-inventario/plantillas");
        anon.Headers.Add("X-Test-Anonymous", "true");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(anon)).StatusCode);
    }

    [Fact]
    public async Task SerieDiarioInv_ExisteYSiguienteNumeroEs000001()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _fixture.AppConnectionString,
                ["ConnectionStrings:IdentityConnection"] = _fixture.IdentityConnectionString,
                ["Database:ApplyMigrationsOnStartup"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddApplicationData(configuration);
        await using var provider = services.BuildServiceProvider();

        await using (var contexto = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_fixture.AppConnectionString).Options))
        {
            await contexto.Database.MigrateAsync();
        }

        await using var scope = provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDbSession>();
        var generador = scope.ServiceProvider.GetRequiredService<IGeneradorNumeroDocumento>();

        await using var tx = await session.BeginTransactionAsync();
        var siguiente = await generador.SiguienteAsync("DIARIO-INV", Hoy);

        Assert.True(siguiente.EsExito, siguiente.EsFallo ? siguiente.Errores[0].Codigo : "");
        Assert.Equal("000001", siguiente.Valor);

        await session.RollbackAsync();
    }

    // ----- Lotes -----

    [Fact]
    public async Task Lotes_Crud_And_Authorization_Work()
    {
        // _client es una única instancia compartida (ver CreateClient): cada llamada se hace con el cliente del rol
        // recién creado, nunca con una referencia cacheada de un rol distinto (los headers se pisarían).

        // Ejecutor tiene CanAdd; Supervisor no.
        var forbidden = await CreateClient("Supervisor").PostAsJsonAsync(
            "/api/diarios-inventario/lotes",
            new { plantillaDiarioId = PlantillaDiarioIds.Articulo, codigo = $"F{Guid.NewGuid():N}"[..10], nombre = "No debería crearse", serieId = (Guid?)null, bloqueado = false });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var codigo = $"LOTE{Guid.NewGuid():N}"[..15];
        var create = await CreateClient("Ejecutor").PostAsJsonAsync(
            "/api/diarios-inventario/lotes",
            new { plantillaDiarioId = PlantillaDiarioIds.Articulo, codigo, nombre = "Lote de prueba", serieId = (Guid?)null, bloqueado = false });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var creado = await create.Content.ReadFromJsonAsync<LoteDiarioResponse>();
        Assert.Equal(codigo.ToUpperInvariant(), creado!.Codigo);
        Assert.Equal(0, creado.NumeroLineas);
        Assert.True(creado.Xmin > 0);

        // Supervisor puede consultar.
        Assert.Equal(HttpStatusCode.OK, (await CreateClient("Supervisor").GetAsync($"/api/diarios-inventario/lotes/{creado.Id}")).StatusCode);

        var lista = await CreateClient("Administrador").GetAsync($"/api/diarios-inventario/lotes?codigo={codigo}");
        Assert.Equal(HttpStatusCode.OK, lista.StatusCode);
        var pagina = await lista.Content.ReadFromJsonAsync<PagedResult<LoteDiarioResponse>>();
        Assert.Contains(pagina!.Items, x => x.Id == creado.Id);

        // Ejecutor no tiene CanModify.
        var putForbidden = await CreateClient("Ejecutor").PutAsJsonAsync(
            $"/api/diarios-inventario/lotes/{creado.Id}",
            new { codigo, nombre = "Renombrado", serieId = (Guid?)null, bloqueado = (bool?)null, xmin = creado.Xmin });
        Assert.Equal(HttpStatusCode.Forbidden, putForbidden.StatusCode);

        // Supervisor SÍ tiene CanModify.
        var update = await CreateClient("Supervisor").PutAsJsonAsync(
            $"/api/diarios-inventario/lotes/{creado.Id}",
            new { codigo, nombre = "Lote renombrado", serieId = (Guid?)null, bloqueado = (bool?)null, xmin = creado.Xmin });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var actualizado = await update.Content.ReadFromJsonAsync<LoteDiarioResponse>();
        Assert.Equal("Lote renombrado", actualizado!.Nombre);
        Assert.True(actualizado.Xmin != creado.Xmin);

        // PUT con el Xmin viejo (desactualizado) -> 409.
        var conflicto = await CreateClient("Administrador").PutAsJsonAsync(
            $"/api/diarios-inventario/lotes/{creado.Id}",
            new { codigo, nombre = "Otro nombre", serieId = (Guid?)null, bloqueado = (bool?)null, xmin = creado.Xmin });
        Assert.Equal(HttpStatusCode.Conflict, conflicto.StatusCode);

        // Solo Administrador puede borrar.
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Supervisor").DeleteAsync($"/api/diarios-inventario/lotes/{creado.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await CreateClient("Administrador").DeleteAsync($"/api/diarios-inventario/lotes/{creado.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await CreateClient("Administrador").GetAsync($"/api/diarios-inventario/lotes/{creado.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await CreateClient("Administrador").DeleteAsync($"/api/diarios-inventario/lotes/{creado.Id}")).StatusCode);
    }

    [Fact]
    public async Task Lotes_CodigoDuplicado_EnLaMismaPlantilla_Devuelve409_YEnOtraPlantilla201()
    {
        var client = CreateClient("Administrador");
        var codigo = $"DUP{Guid.NewGuid():N}"[..15];

        var primero = await CrearLoteAsync(client, PlantillaDiarioIds.Articulo, codigo);
        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);

        var duplicadoMismaPlantilla = await CrearLoteAsync(client, PlantillaDiarioIds.Articulo, codigo);
        Assert.Equal(HttpStatusCode.Conflict, duplicadoMismaPlantilla.StatusCode);

        var otraPlantilla = await CrearLoteAsync(client, PlantillaDiarioIds.Reclasificacion, codigo);
        Assert.Equal(HttpStatusCode.Created, otraPlantilla.StatusCode);
    }

    [Fact]
    public async Task Lotes_PlantillaInexistente_Devuelve400()
    {
        var client = CreateClient("Administrador");
        var response = await CrearLoteAsync(client, Guid.NewGuid(), $"X{Guid.NewGuid():N}"[..10]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problema = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.True(problema!.RootElement.GetProperty("errors").TryGetProperty("PlantillaDiarioId", out _));
    }

    [Fact]
    public async Task Lotes_BorrarConLineas_Devuelve409_YSinLineas204()
    {
        var client = CreateClient("Administrador");
        var loteId = await CrearLoteOkAsync(client, PlantillaDiarioIds.Articulo);
        var (productoId, almacenId, _) = await CrearProductoYAlmacenesAsync(client);

        var linea = await CrearLineaAjustePositivoAsync(client, loteId, productoId, almacenId);
        Assert.Equal(HttpStatusCode.Created, linea.StatusCode);

        var deleteConLineas = await client.DeleteAsync($"/api/diarios-inventario/lotes/{loteId}");
        Assert.Equal(HttpStatusCode.Conflict, deleteConLineas.StatusCode);
        var problema = await deleteConLineas.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.True(problema!.RootElement.GetProperty("errors").TryGetProperty("Id", out _));

        var lineaCreada = await linea.Content.ReadFromJsonAsync<LineaDiarioResponse>();
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/diarios-inventario/lineas/{lineaCreada!.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/diarios-inventario/lotes/{loteId}")).StatusCode);
    }

    // ----- Líneas -----

    [Fact]
    public async Task Lineas_Crud_NumeroLineaYCamposDeUnion_YAutorizacion()
    {
        var loteId = await CrearLoteOkAsync(CreateClient("Administrador"), PlantillaDiarioIds.Articulo);
        var (productoId, almacenId, _) = await CrearProductoYAlmacenesAsync(CreateClient("Administrador"));

        // Supervisor no tiene CanAdd.
        Assert.Equal(HttpStatusCode.Forbidden, (await CrearLineaAjustePositivoAsync(CreateClient("Supervisor"), loteId, productoId, almacenId)).StatusCode);

        var primera = await CrearLineaAjustePositivoAsync(CreateClient("Ejecutor"), loteId, productoId, almacenId);
        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
        var lineaUno = await primera.Content.ReadFromJsonAsync<LineaDiarioResponse>();
        Assert.Equal(10000, lineaUno!.NumeroLinea);
        Assert.Equal(50m, lineaUno.ImporteCosto); // Cantidad 10 * factor 1 * costo 5
        Assert.Equal(1m, lineaUno.CantidadPorUnidadMedida);
        Assert.NotNull(lineaUno.ProductoCodigo);
        Assert.NotNull(lineaUno.ProductoNombre);
        Assert.NotNull(lineaUno.AlmacenCodigo);
        Assert.Null(lineaUno.AlmacenDestinoCodigo);
        Assert.Equal("UND", lineaUno.UnidadMedidaCodigo);

        var segunda = await CrearLineaAjustePositivoAsync(CreateClient("Administrador"), loteId, productoId, almacenId);
        var lineaDos = await segunda.Content.ReadFromJsonAsync<LineaDiarioResponse>();
        Assert.Equal(20000, lineaDos!.NumeroLinea);

        var listado = await CreateClient("Supervisor").GetAsync($"/api/diarios-inventario/lotes/{loteId}/lineas");
        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
        var lineas = await listado.Content.ReadFromJsonAsync<List<LineaDiarioResponse>>();
        Assert.Equal([10000, 20000], lineas!.Select(l => l.NumeroLinea));

        // Ejecutor no tiene CanModify.
        var putForbidden = await CreateClient("Ejecutor").PutAsJsonAsync(
            $"/api/diarios-inventario/lineas/{lineaUno.Id}",
            NuevoCuerpoLinea(productoId, almacenId, null, TipoMovimientoInventario.AjustePositivo, 5m, 2m, lineaUno.Xmin));
        Assert.Equal(HttpStatusCode.Forbidden, putForbidden.StatusCode);

        var putOk = await CreateClient("Supervisor").PutAsJsonAsync(
            $"/api/diarios-inventario/lineas/{lineaUno.Id}",
            NuevoCuerpoLinea(productoId, almacenId, null, TipoMovimientoInventario.AjustePositivo, 5m, 2m, lineaUno.Xmin));
        Assert.Equal(HttpStatusCode.OK, putOk.StatusCode);
        var lineaActualizada = await putOk.Content.ReadFromJsonAsync<LineaDiarioResponse>();
        Assert.Equal(10m, lineaActualizada!.ImporteCosto); // 5 * 1 * 2
        Assert.Equal(10000, lineaActualizada.NumeroLinea); // inmutable

        // PUT con Xmin desactualizado -> 409.
        var conflicto = await CreateClient("Administrador").PutAsJsonAsync(
            $"/api/diarios-inventario/lineas/{lineaUno.Id}",
            NuevoCuerpoLinea(productoId, almacenId, null, TipoMovimientoInventario.AjustePositivo, 5m, 3m, lineaUno.Xmin));
        Assert.Equal(HttpStatusCode.Conflict, conflicto.StatusCode);

        // Solo Administrador borra.
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Supervisor").DeleteAsync($"/api/diarios-inventario/lineas/{lineaUno.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await CreateClient("Administrador").DeleteAsync($"/api/diarios-inventario/lineas/{lineaUno.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await CreateClient("Administrador").GetAsync($"/api/diarios-inventario/lineas/{lineaUno.Id}")).StatusCode);
    }

    [Fact]
    public async Task Lineas_ValidacionesDeCaptura_DevuelvenCodigoYCampoEsperados()
    {
        var client = CreateClient("Administrador");
        var loteId = await CrearLoteOkAsync(client, PlantillaDiarioIds.Articulo);
        var (productoId, almacenId, almacenDestinoId) = await CrearProductoYAlmacenesAsync(client);

        // Tipo de movimiento no permitido por la plantilla (Articulo no admite Transferencia).
        var tipoInvalido = await client.PostAsJsonAsync(
            $"/api/diarios-inventario/lotes/{loteId}/lineas",
            NuevaLinea(productoId, almacenId, almacenDestinoId, TipoMovimientoInventario.Transferencia, 1m, null));
        await AssertErrorAsync(tipoInvalido, HttpStatusCode.BadRequest, "TipoMovimiento");

        // Ajuste positivo sin costo.
        var sinCosto = await client.PostAsJsonAsync(
            $"/api/diarios-inventario/lotes/{loteId}/lineas",
            NuevaLinea(productoId, almacenId, null, TipoMovimientoInventario.AjustePositivo, 1m, null));
        await AssertErrorAsync(sinCosto, HttpStatusCode.BadRequest, "CostoUnitario");

        // Cantidad inválida.
        var cantidadInvalida = await client.PostAsJsonAsync(
            $"/api/diarios-inventario/lotes/{loteId}/lineas",
            NuevaLinea(productoId, almacenId, null, TipoMovimientoInventario.AjustePositivo, 0m, 1m));
        await AssertErrorAsync(cantidadInvalida, HttpStatusCode.BadRequest, "Cantidad");

        // Producto inexistente.
        var productoInvalido = await client.PostAsJsonAsync(
            $"/api/diarios-inventario/lotes/{loteId}/lineas",
            NuevaLinea(Guid.NewGuid(), almacenId, null, TipoMovimientoInventario.AjustePositivo, 1m, 1m));
        await AssertErrorAsync(productoInvalido, HttpStatusCode.BadRequest, "ProductoId");

        // Lote inexistente (por URL) -> 404, no 400: LoteDiarioId es un recurso de la URL, no una referencia del
        // cuerpo (Ronda de corrección 1; el handler lo carga y bloquea ANTES de invocar las reglas de validación).
        var loteInexistente = await client.PostAsJsonAsync(
            $"/api/diarios-inventario/lotes/{Guid.NewGuid()}/lineas",
            NuevaLinea(productoId, almacenId, null, TipoMovimientoInventario.AjustePositivo, 1m, 1m));
        await AssertErrorAsync(loteInexistente, HttpStatusCode.NotFound, "LoteDiarioId");

        // Cantidad mayor que el máximo de numeric(18,6): sin este límite, Postgres respondería 500
        // (22003 numeric_field_overflow) en vez de un 400 legible.
        var cantidadDesborda = await client.PostAsJsonAsync(
            $"/api/diarios-inventario/lotes/{loteId}/lineas",
            NuevaLinea(productoId, almacenId, null, TipoMovimientoInventario.AjustePositivo, 1_000_000_000_000m, 1m));
        await AssertErrorAsync(cantidadDesborda, HttpStatusCode.BadRequest, "Cantidad");

        // Cantidad y costo válidos por separado, pero cuyo PRODUCTO desborda numeric(18,4) (1e6 * 1e10 = 1e16).
        var importeDesborda = await client.PostAsJsonAsync(
            $"/api/diarios-inventario/lotes/{loteId}/lineas",
            NuevaLinea(productoId, almacenId, null, TipoMovimientoInventario.AjustePositivo, 1_000_000m, 10_000_000_000m));
        await AssertErrorAsync(importeDesborda, HttpStatusCode.BadRequest, "CostoUnitario");

        // Fechas obligatorias: un cuerpo que las omite deserializa DateOnly como default(DateOnly), no lanza.
        var sinFechas = await client.PostAsync(
            $"/api/diarios-inventario/lotes/{loteId}/lineas",
            new StringContent(
                $$"""
                {"numeroDocumento":null,"tipoMovimiento":3,"productoId":"{{productoId}}","almacenId":"{{almacenId}}",
                 "almacenDestinoId":null,"unidadMedidaId":"{{UnidadUnd}}","cantidad":1,"costoUnitario":1,"descripcion":null}
                """,
                System.Text.Encoding.UTF8, "application/json"));
        await AssertErrorAsync(sinFechas, HttpStatusCode.BadRequest, "FechaRegistro");
    }

    [Fact]
    public async Task Lineas_AltasConcurrentesEnElMismoLote_AmbasCreanConNumeroLineaDistinto()
    {
        var client = CreateClient("Administrador");
        var loteId = await CrearLoteOkAsync(client, PlantillaDiarioIds.Articulo);
        var (productoId, almacenId, _) = await CrearProductoYAlmacenesAsync(client);

        // Ronda de corrección 1: el FOR UPDATE sobre la fila del lote serializa las dos altas -> ambas 201, nunca un
        // 23505 confuso ni un NumeroLinea repetido.
        var respuestas = await Task.WhenAll(
            CrearLineaAjustePositivoAsync(CreateClient("Administrador"), loteId, productoId, almacenId),
            CrearLineaAjustePositivoAsync(CreateClient("Administrador"), loteId, productoId, almacenId));

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

        var numeros = await Task.WhenAll(respuestas.Select(async r => (await r.Content.ReadFromJsonAsync<LineaDiarioResponse>())!.NumeroLinea));
        Assert.Equal(2, numeros.Distinct().Count());
        Assert.Equal([10000, 20000], numeros.OrderBy(n => n));
    }

    [Fact]
    public async Task Lotes_SerieQueNoEsDeDiario_Devuelve400()
    {
        var client = CreateClient("Administrador");
        var serieSociosId = await ObtenerSerieSociosIdAsync();

        var response = await client.PostAsJsonAsync(
            "/api/diarios-inventario/lotes",
            new { plantillaDiarioId = PlantillaDiarioIds.Articulo, codigo = $"S{Guid.NewGuid():N}"[..10], nombre = "Lote", serieId = serieSociosId, bloqueado = false });

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "SerieId");
    }

    [Fact]
    public async Task ListLineas_ConLoteInexistente_Devuelve404()
    {
        var client = CreateClient("Administrador");

        var response = await client.GetAsync($"/api/diarios-inventario/lotes/{Guid.NewGuid()}/lineas");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Guid> ObtenerSerieSociosIdAsync()
    {
        await using var contexto = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_fixture.AppConnectionString).Options);
        return await contexto.Series.Where(s => s.Codigo == "SOCIOS").Select(s => s.Id).SingleAsync();
    }

    [Fact]
    public async Task Lineas_LoteBloqueado_RechazaNuevasLineas()
    {
        var client = CreateClient("Administrador");
        var loteId = await CrearLoteOkAsync(client, PlantillaDiarioIds.Articulo, bloqueado: true);
        var (productoId, almacenId, _) = await CrearProductoYAlmacenesAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/diarios-inventario/lotes/{loteId}/lineas",
            NuevaLinea(productoId, almacenId, null, TipoMovimientoInventario.AjustePositivo, 1m, 1m));

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "LoteDiarioId");
    }

    [Fact]
    public async Task Lineas_Reclasificacion_ConAlmacenDestinoValido_Devuelve201ConCodigoDestino()
    {
        var client = CreateClient("Administrador");
        var loteId = await CrearLoteOkAsync(client, PlantillaDiarioIds.Reclasificacion);
        var (productoId, almacenId, almacenDestinoId) = await CrearProductoYAlmacenesAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/diarios-inventario/lotes/{loteId}/lineas",
            NuevaLinea(productoId, almacenId, almacenDestinoId, TipoMovimientoInventario.Transferencia, 5m, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var linea = await response.Content.ReadFromJsonAsync<LineaDiarioResponse>();
        Assert.NotNull(linea!.AlmacenDestinoCodigo);
        Assert.Equal(0m, linea.ImporteCosto);

        // Origen == destino -> inválido.
        var mismoAlmacen = await client.PostAsJsonAsync(
            $"/api/diarios-inventario/lotes/{loteId}/lineas",
            NuevaLinea(productoId, almacenId, almacenId, TipoMovimientoInventario.Transferencia, 5m, null));
        await AssertErrorAsync(mismoAlmacen, HttpStatusCode.BadRequest, "AlmacenDestinoId");
    }

    // ----- Registro (Task 4.3) -----

    [Fact]
    public async Task Registrar_EjecutorRecibe403_Supervisor200_YGetRegistrosLoLista()
    {
        var loteId = await CrearLoteOkAsync(CreateClient("Administrador"), PlantillaDiarioIds.Articulo);
        var (productoId, almacenId, _) = await CrearProductoYAlmacenesAsync(CreateClient("Administrador"));
        var alta = await CrearLineaAjustePositivoAsync(CreateClient("Administrador"), loteId, productoId, almacenId);
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var linea = await alta.Content.ReadFromJsonAsync<LineaDiarioResponse>();

        // Registrar = CanModify: Ejecutor (CanAdd, sin CanModify) no puede.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await CreateClient("Ejecutor").PostAsync($"/api/diarios-inventario/lotes/{loteId}/registrar", null)).StatusCode);

        var registrar = await CreateClient("Supervisor").PostAsync($"/api/diarios-inventario/lotes/{loteId}/registrar", null);
        Assert.Equal(HttpStatusCode.OK, registrar.StatusCode);
        var resultado = await registrar.Content.ReadFromJsonAsync<ResultadoRegistroLote>();
        Assert.NotNull(resultado);
        Assert.Matches("^[0-9]{6}$", resultado!.NumeroRegistro);
        Assert.Equal(1, resultado.Movimientos);
        Assert.Equal(resultado.DesdeMovimientoProducto, resultado.HastaMovimientoProducto);

        var existencias = JsonDocument.Parse(await (await CreateClient("Supervisor")
            .GetAsync($"/api/productos/{productoId}/existencias")).Content.ReadAsStringAsync());
        Assert.Contains(existencias.RootElement.EnumerateArray(), e =>
            e.GetProperty("almacenId").GetGuid() == almacenId && e.GetProperty("existencia").GetDecimal() == 10m);

        // Consultar = CanConsult: también el Ejecutor.
        var listado = await CreateClient("Ejecutor").GetAsync($"/api/diarios-inventario/registros?loteId={loteId}");
        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
        var pagina = await listado.Content.ReadFromJsonAsync<PagedResult<RegistroDiarioResponse>>();
        var registro = Assert.Single(pagina!.Items);
        Assert.Equal(resultado.NumeroRegistro, registro.NumeroRegistro);
        Assert.Equal(1, registro.Lineas);
        Assert.Equal((resultado.DesdeMovimientoProducto, resultado.HastaMovimientoProducto), (registro.DesdeMovimientoProducto, registro.HastaMovimientoProducto));
        Assert.Equal("supervisor", registro.CreadoPor);
        Assert.NotNull(registro.LoteDiarioCodigo);

        // Review Focus 3 y 4: la segunda vez el lote está vacío (400), y la línea registrada ya no existe (PUT/GET 404).
        await AssertErrorAsync(
            await CreateClient("Supervisor").PostAsync($"/api/diarios-inventario/lotes/{loteId}/registrar", null),
            HttpStatusCode.BadRequest, "LoteDiarioId");
        var put = await CreateClient("Supervisor").PutAsJsonAsync(
            $"/api/diarios-inventario/lineas/{linea!.Id}",
            NuevoCuerpoLinea(productoId, almacenId, null, TipoMovimientoInventario.AjustePositivo, 1m, 1m, linea.Xmin));
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await CreateClient("Administrador").GetAsync($"/api/diarios-inventario/lineas/{linea.Id}")).StatusCode);
    }

    [Fact]
    public async Task Registrar_LoteInexistente404_YLineaInvalidaDevuelveCampoDeLaLinea()
    {
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await CreateClient("Administrador").PostAsync($"/api/diarios-inventario/lotes/{Guid.NewGuid()}/registrar", null)).StatusCode);

        var loteId = await CrearLoteOkAsync(CreateClient("Administrador"), PlantillaDiarioIds.Articulo);
        var (productoId, almacenId, _) = await CrearProductoYAlmacenesAsync(CreateClient("Administrador"));
        Assert.Equal(
            HttpStatusCode.Created,
            (await CreateClient("Administrador").PostAsJsonAsync(
                $"/api/diarios-inventario/lotes/{loteId}/lineas",
                NuevaLinea(productoId, almacenId, null, TipoMovimientoInventario.AjusteNegativo, 5m, null))).StatusCode);

        // Ajuste negativo sin existencia: 400 con el campo de la línea y sin escribir nada.
        var registrar = await CreateClient("Administrador").PostAsync($"/api/diarios-inventario/lotes/{loteId}/registrar", null);
        await AssertErrorAsync(registrar, HttpStatusCode.BadRequest, "Lineas[10000].Cantidad");
        var listado = await CreateClient("Administrador").GetAsync($"/api/diarios-inventario/registros?loteId={loteId}");
        Assert.Empty((await listado.Content.ReadFromJsonAsync<PagedResult<RegistroDiarioResponse>>())!.Items);
    }

    private static object NuevaLinea(
        Guid productoId, Guid almacenId, Guid? almacenDestinoId, TipoMovimientoInventario tipo, decimal cantidad, decimal? costoUnitario) => new
    {
        fechaRegistro = Hoy,
        fechaDocumento = Hoy,
        numeroDocumento = (string?)null,
        tipoMovimiento = tipo,
        productoId,
        almacenId,
        almacenDestinoId,
        unidadMedidaId = UnidadUnd,
        cantidad,
        costoUnitario,
        descripcion = "Línea de prueba"
    };

    private static object NuevoCuerpoLinea(
        Guid productoId, Guid almacenId, Guid? almacenDestinoId, TipoMovimientoInventario tipo, decimal cantidad, decimal? costoUnitario, long xmin) => new
    {
        fechaRegistro = Hoy,
        fechaDocumento = Hoy,
        numeroDocumento = (string?)null,
        tipoMovimiento = tipo,
        productoId,
        almacenId,
        almacenDestinoId,
        unidadMedidaId = UnidadUnd,
        cantidad,
        costoUnitario,
        descripcion = "Línea actualizada",
        xmin
    };

    private static Task<HttpResponseMessage> CrearLineaAjustePositivoAsync(HttpClient client, Guid loteId, Guid productoId, Guid almacenId) =>
        client.PostAsJsonAsync(
            $"/api/diarios-inventario/lotes/{loteId}/lineas",
            NuevaLinea(productoId, almacenId, null, TipoMovimientoInventario.AjustePositivo, 10m, 5m));

    private static Task<HttpResponseMessage> CrearLoteAsync(HttpClient client, Guid plantillaId, string codigo, bool bloqueado = false) =>
        client.PostAsJsonAsync(
            "/api/diarios-inventario/lotes",
            new { plantillaDiarioId = plantillaId, codigo, nombre = "Lote de prueba", serieId = (Guid?)null, bloqueado });

    private static async Task<Guid> CrearLoteOkAsync(HttpClient client, Guid plantillaId, bool bloqueado = false)
    {
        var response = await CrearLoteAsync(client, plantillaId, $"L{Guid.NewGuid():N}"[..15], bloqueado);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var lote = await response.Content.ReadFromJsonAsync<LoteDiarioResponse>();
        return lote!.Id;
    }

    private static async Task<(Guid ProductoId, Guid AlmacenId, Guid AlmacenDestinoId)> CrearProductoYAlmacenesAsync(HttpClient client)
    {
        var codigoProducto = $"PR{Guid.NewGuid():N}"[..15];
        var crearProducto = await client.PostAsJsonAsync(
            "/api/productos", new { codigo = codigoProducto, nombre = "Producto de diario", precioVenta = 1m });
        Assert.Equal(HttpStatusCode.Created, crearProducto.StatusCode);
        var productoId = JsonDocument.Parse(await crearProducto.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        var almacenOrigen = await CrearAlmacenAsync(client, "AO");
        var almacenDestino = await CrearAlmacenAsync(client, "AD");

        return (productoId, almacenOrigen, almacenDestino);
    }

    private static async Task<Guid> CrearAlmacenAsync(HttpClient client, string prefijo)
    {
        var codigo = $"{prefijo}{Guid.NewGuid():N}"[..10];
        var crear = await client.PostAsJsonAsync(
            "/api/almacenes", new { codigo, nombre = $"Almacén {prefijo}", bloqueado = false, esPredeterminado = false });
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);
        return JsonDocument.Parse(await crear.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string campo)
    {
        Assert.Equal(status, response.StatusCode);
        var problema = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.True(
            problema!.RootElement.GetProperty("errors").TryGetProperty(campo, out _),
            $"Se esperaba el campo '{campo}' en los errores: {problema.RootElement}");
    }

    private HttpClient CreateClient(string role)
    {
        var client = _client;
        client.DefaultRequestHeaders.Remove("X-Test-Anonymous");
        client.DefaultRequestHeaders.Remove("X-Test-User");
        client.DefaultRequestHeaders.Remove("X-Test-Roles");
        client.DefaultRequestHeaders.Add("X-Test-User", role.ToLowerInvariant());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return client;
    }
}
