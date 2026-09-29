using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using OpenSource1.Application.Features.Almacenes.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Infrastructure.Data;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

[Collection(PostgresCollection.Name)]
public sealed class AlmacenesApiTests : IClassFixture<PostgresTestFixture>
{
    // Semilla de unidad "UND" (Task 2.x): usada para insertar por SQL un MovimientoProducto de prueba sin
    // depender de crear una unidad de medida propia.
    private static readonly Guid UnidadUndSemilla = Guid.Parse("a1000000-0000-0000-0000-000000000001");

    private readonly PostgresTestFixture _fixture;
    private readonly HttpClient _client;

    public AlmacenesApiTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
        var factory = fixture.CreateFactory();
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Crud_And_Authorization_Work()
    {
        var anon = new HttpRequestMessage(HttpMethod.Get, "/api/almacenes");
        anon.Headers.Add("X-Test-Anonymous", "true");
        var anonymousResponse = await _client.SendAsync(anon);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        var supervisor = CreateClient("Supervisor");
        var forbidden = await supervisor.PostAsJsonAsync(
            "/api/almacenes",
            new { codigo = $"F{Guid.NewGuid():N}"[..10], nombre = "No debería crearse", bloqueado = false, esPredeterminado = false });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // Supervisor SÍ puede consultar.
        Assert.Equal(HttpStatusCode.OK, (await supervisor.GetAsync("/api/almacenes")).StatusCode);

        var client = CreateClient("Administrador");
        var codigo = $"T{Guid.NewGuid():N}"[..10];
        var create = await client.PostAsJsonAsync(
            "/api/almacenes",
            new { codigo, nombre = "Sucursal Norte", ciudad = "Santiago", paisCodigo = "DO", bloqueado = false, esPredeterminado = false });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var creado = await create.Content.ReadFromJsonAsync<AlmacenResponse>();
        Assert.Equal(codigo.ToUpperInvariant(), creado!.Codigo);
        Assert.Equal("Santiago", creado.Ciudad);
        Assert.Equal("DO", creado.PaisCodigo);

        var list = await client.GetAsync("/api/almacenes?tamanoPagina=200");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var paged = await list.Content.ReadFromJsonAsync<PagedResult<AlmacenResponse>>();
        Assert.NotNull(paged);
        Assert.Contains(paged!.Items, x => x.Id == creado.Id);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/almacenes/{creado.Id}")).StatusCode);

        var update = await client.PutAsJsonAsync(
            $"/api/almacenes/{creado.Id}",
            new { codigo, nombre = "Sucursal Norte (renombrada)", direccionLinea1 = (string?)null, direccionLinea2 = (string?)null, ciudad = (string?)null, paisCodigo = (string?)null, bloqueado = (bool?)null, esPredeterminado = (bool?)null });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var actualizado = await update.Content.ReadFromJsonAsync<AlmacenResponse>();
        Assert.Equal("Sucursal Norte (renombrada)", actualizado!.Nombre);
        // Campos no informados (null) se conservan.
        Assert.Equal("Santiago", actualizado.Ciudad);
        Assert.Equal("DO", actualizado.PaisCodigo);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/almacenes/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/almacenes/{Guid.NewGuid()}")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Supervisor").DeleteAsync($"/api/almacenes/{creado.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Ejecutor").DeleteAsync($"/api/almacenes/{creado.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await CreateClient("Administrador").DeleteAsync($"/api/almacenes/{creado.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/almacenes/{creado.Id}")).StatusCode);

        var listaTrasBorrar = await ListarPorCodigoAsync(client, codigo);
        Assert.Equal(0, listaTrasBorrar.Total);
    }

    [Fact]
    public async Task Create_NormalizaElCodigoAMayusculas()
    {
        var client = CreateClient("Administrador");
        var codigo = $" pri2{Guid.NewGuid():N}"[..8];

        var create = await client.PostAsJsonAsync(
            "/api/almacenes", new { codigo, nombre = "Quintal", bloqueado = false, esPredeterminado = false });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var creado = await create.Content.ReadFromJsonAsync<AlmacenResponse>();
        Assert.Equal(codigo.Trim().ToUpperInvariant(), creado!.Codigo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A B")]
    [InlineData("CODIGOMUYLARGO")]
    public async Task Create_ConCodigoInvalido_Devuelve400ConCampoCodigo(string codigo)
    {
        var client = CreateClient("Administrador");

        var response = await client.PostAsJsonAsync(
            "/api/almacenes", new { codigo, nombre = "Nombre", bloqueado = false, esPredeterminado = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problema = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.True(problema!.RootElement.GetProperty("errors").TryGetProperty("Codigo", out _));
    }

    [Fact]
    public async Task Create_ConCodigoDuplicado_Devuelve409()
    {
        var client = CreateClient("Administrador");
        var codigo = $"D{Guid.NewGuid():N}"[..10];

        var primero = await client.PostAsJsonAsync(
            "/api/almacenes", new { codigo, nombre = "Original", bloqueado = false, esPredeterminado = false });
        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);

        var duplicado = await client.PostAsJsonAsync(
            "/api/almacenes", new { codigo, nombre = "Duplicado", bloqueado = false, esPredeterminado = false });
        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);
    }

    [Fact]
    public async Task Create_TrasBorrarElMismoCodigo_NoChocaConElIndiceUnico_YDevuelve201()
    {
        var client = CreateClient("Administrador");
        var codigo = $"B{Guid.NewGuid():N}"[..10];

        var create = await client.PostAsJsonAsync(
            "/api/almacenes", new { codigo, nombre = "Uno", bloqueado = false, esPredeterminado = false });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var creadoId = (await create.Content.ReadFromJsonAsync<AlmacenResponse>())!.Id;

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/almacenes/{creadoId}")).StatusCode);

        var recreate = await client.PostAsJsonAsync(
            "/api/almacenes", new { codigo, nombre = "Dos", bloqueado = false, esPredeterminado = false });

        Assert.Equal(HttpStatusCode.Created, recreate.StatusCode);
        Assert.NotEqual(HttpStatusCode.InternalServerError, recreate.StatusCode);
    }

    [Fact]
    public async Task Create_ConEsPredeterminadoTrue_DesmarcaElAnteriorPredeterminado()
    {
        var client = CreateClient("Administrador");
        var codigoA = $"PA{Guid.NewGuid():N}"[..10];
        var codigoB = $"PB{Guid.NewGuid():N}"[..10];

        var createA = await client.PostAsJsonAsync(
            "/api/almacenes", new { codigo = codigoA, nombre = "A", bloqueado = false, esPredeterminado = true });
        Assert.Equal(HttpStatusCode.Created, createA.StatusCode);
        var idA = (await createA.Content.ReadFromJsonAsync<AlmacenResponse>())!.Id;

        var createB = await client.PostAsJsonAsync(
            "/api/almacenes", new { codigo = codigoB, nombre = "B", bloqueado = false, esPredeterminado = true });
        Assert.Equal(HttpStatusCode.Created, createB.StatusCode);
        var creadoB = await createB.Content.ReadFromJsonAsync<AlmacenResponse>();
        Assert.True(creadoB!.EsPredeterminado);

        var getA = await client.GetAsync($"/api/almacenes/{idA}");
        var almacenA = await getA.Content.ReadFromJsonAsync<AlmacenResponse>();
        Assert.False(almacenA!.EsPredeterminado);

        var getB = await client.GetAsync($"/api/almacenes/{creadoB.Id}");
        var almacenB = await getB.Content.ReadFromJsonAsync<AlmacenResponse>();
        Assert.True(almacenB!.EsPredeterminado);
    }

    [Fact]
    public async Task Update_EsPredeterminadoFalse_SobreElPredeterminadoActual_Devuelve400PredeterminadoRequerido()
    {
        var client = CreateClient("Administrador");
        var codigo = $"UP{Guid.NewGuid():N}"[..10];

        var create = await client.PostAsJsonAsync(
            "/api/almacenes", new { codigo, nombre = "Predeterminado", bloqueado = false, esPredeterminado = true });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<AlmacenResponse>())!.Id;

        var update = await client.PutAsJsonAsync(
            $"/api/almacenes/{id}",
            new { codigo, nombre = "Predeterminado", direccionLinea1 = (string?)null, direccionLinea2 = (string?)null, ciudad = (string?)null, paisCodigo = (string?)null, bloqueado = (bool?)null, esPredeterminado = false });

        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        var problema = await update.Content.ReadFromJsonAsync<JsonDocument>();
        var mensajes = problema!.RootElement.GetProperty("errors").GetProperty("EsPredeterminado");
        Assert.Contains("predeterminado", mensajes[0].GetString(), StringComparison.OrdinalIgnoreCase);

        // Sigue siendo el predeterminado.
        var get = await client.GetAsync($"/api/almacenes/{id}");
        var almacen = await get.Content.ReadFromJsonAsync<AlmacenResponse>();
        Assert.True(almacen!.EsPredeterminado);
    }

    [Fact]
    public async Task Delete_DelAlmacenPredeterminado_Devuelve409()
    {
        var client = CreateClient("Administrador");
        var codigo = $"DP{Guid.NewGuid():N}"[..10];

        var create = await client.PostAsJsonAsync(
            "/api/almacenes", new { codigo, nombre = "Predeterminado", bloqueado = false, esPredeterminado = true });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<AlmacenResponse>())!.Id;

        var delete = await client.DeleteAsync($"/api/almacenes/{id}");

        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        // Sigue existiendo.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/almacenes/{id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_ConMovimientosDeInventario_Devuelve409YElAlmacenSigueExistiendo()
    {
        var client = CreateClient("Administrador");
        var codigo = $"DM{Guid.NewGuid():N}"[..10];

        var create = await client.PostAsJsonAsync(
            "/api/almacenes", new { codigo, nombre = "Con movimientos", bloqueado = false, esPredeterminado = false });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var almacenId = (await create.Content.ReadFromJsonAsync<AlmacenResponse>())!.Id;

        var codigoProducto = $"MP{Guid.NewGuid():N}"[..15];
        var crearProducto = await client.PostAsJsonAsync(
            "/api/productos", new { codigo = codigoProducto, nombre = "Producto para movimiento", precioVenta = 1m, stock = 0 });
        Assert.Equal(HttpStatusCode.Created, crearProducto.StatusCode);
        var productoId = JsonDocument.Parse(await crearProducto.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        await InsertarMovimientoDePruebaAsync(productoId, almacenId);

        var delete = await client.DeleteAsync($"/api/almacenes/{almacenId}");

        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        var problema = await delete.Content.ReadFromJsonAsync<JsonDocument>();
        var mensajes = problema!.RootElement.GetProperty("errors").GetProperty("Id");
        Assert.Contains("movimientos", mensajes[0].GetString(), StringComparison.OrdinalIgnoreCase);

        // Sigue existiendo: el borrado lógico no se aplicó.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/almacenes/{almacenId}")).StatusCode);
    }

    [Fact]
    public async Task Delete_Normal_Devuelve204YLuegoGet404YElListadoNoLoDevuelve()
    {
        var client = CreateClient("Administrador");
        var codigo = $"DN{Guid.NewGuid():N}"[..10];

        var create = await client.PostAsJsonAsync(
            "/api/almacenes", new { codigo, nombre = "Normal", bloqueado = false, esPredeterminado = false });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<AlmacenResponse>())!.Id;

        var delete = await client.DeleteAsync($"/api/almacenes/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/almacenes/{id}")).StatusCode);

        var lista = await ListarPorCodigoAsync(client, codigo);
        Assert.Equal(0, lista.Total);
    }

    [Fact]
    public async Task Create_ConcurrentesDosAltasPredeterminadas_DejanExactamenteUnPredeterminadoYNunca500()
    {
        var client = CreateClient("Administrador");
        var codigoA = $"CA{Guid.NewGuid():N}"[..10];
        var codigoB = $"CB{Guid.NewGuid():N}"[..10];

        var respuestas = await Task.WhenAll(
            client.PostAsJsonAsync("/api/almacenes", new { codigo = codigoA, nombre = "Concurrente A", bloqueado = false, esPredeterminado = true }),
            client.PostAsJsonAsync("/api/almacenes", new { codigo = codigoB, nombre = "Concurrente B", bloqueado = false, esPredeterminado = true }));

        Assert.All(respuestas, r => Assert.True(
            r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict,
            $"Se esperaba 201 o 409, no {r.StatusCode}."));
        Assert.DoesNotContain(respuestas, r => r.StatusCode == HttpStatusCode.InternalServerError);

        var lista = await client.GetAsync("/api/almacenes?tamanoPagina=200");
        Assert.Equal(HttpStatusCode.OK, lista.StatusCode);
        var paged = await lista.Content.ReadFromJsonAsync<PagedResult<AlmacenResponse>>();
        Assert.Equal(1, paged!.Items.Count(x => x.EsPredeterminado));
    }

    private static async Task<PagedResult<AlmacenResponse>> ListarPorCodigoAsync(HttpClient client, string codigo)
    {
        var response = await client.GetAsync($"/api/almacenes?codigo={codigo}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PagedResult<AlmacenResponse>>())!;
    }

    /// <summary>
    /// Inserta por SQL directo un <c>MovimientoProducto</c> mínimo (Task 3.3): la escritura normal del
    /// libro es del servicio de registro de la Task 3.4, que todavía no existe, así que la guarda de
    /// borrado se prueba con una fila insertada a mano.
    /// </summary>
    private async Task InsertarMovimientoDePruebaAsync(Guid productoId, Guid almacenId)
    {
        await using var contexto = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_fixture.AppConnectionString).Options);

        await contexto.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO "MovimientosProducto"
                ("ProductoId","AlmacenId","TipoMovimiento","TipoDocumento","NumeroLineaDocumento",
                 "FechaRegistro","FechaDocumento","Cantidad","CantidadRestante","CantidadFacturada",
                 "UnidadMedidaId","CantidadPorUnidadMedida","TipoOrigen","ClaveOrigen","CreatedAtUtc","CreatedBy")
            VALUES
                ({productoId},{almacenId},1,1,1,CURRENT_DATE,CURRENT_DATE,10,10,10,{UnidadUndSemilla},1,1,
                 {$"TEST-GUARD-{Guid.NewGuid():N}"},now(),'test')
            """);
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

/// <summary>
/// Verifica la semilla en un contenedor Postgres propio, recién migrado, sin que otros métodos de
/// <see cref="AlmacenesApiTests"/> (que crean/borran almacenes y mueven la marca de predeterminado)
/// puedan haber alterado el estado: mismo motivo de aislamiento que <c>AuthPermissionsApiTests</c>.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AlmacenesSemillaApiTests : IClassFixture<PostgresTestFixture>
{
    private readonly HttpClient _client;

    public AlmacenesSemillaApiTests(PostgresTestFixture fixture)
    {
        var factory = fixture.CreateFactory();
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task LaSemillaPrincipal_ExisteTrasMigrar_YEsLaUnicaPredeterminada()
    {
        var client = _client;
        client.DefaultRequestHeaders.Add("X-Test-User", "administrador");
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Administrador");

        var getPrincipal = await client.GetAsync($"/api/almacenes/{AlmacenIds.Principal}");
        Assert.Equal(HttpStatusCode.OK, getPrincipal.StatusCode);
        var principal = await getPrincipal.Content.ReadFromJsonAsync<AlmacenResponse>();
        Assert.Equal("PRINCIPAL", principal!.Codigo);
        Assert.Equal("Almacén principal", principal.Nombre);
        Assert.True(principal.EsPredeterminado);

        var lista = await client.GetAsync("/api/almacenes?tamanoPagina=200");
        Assert.Equal(HttpStatusCode.OK, lista.StatusCode);
        var paged = await lista.Content.ReadFromJsonAsync<PagedResult<AlmacenResponse>>();
        Assert.Equal(1, paged!.Items.Count(x => x.EsPredeterminado));
        Assert.Equal(AlmacenIds.Principal, Assert.Single(paged.Items, x => x.EsPredeterminado).Id);
    }
}
