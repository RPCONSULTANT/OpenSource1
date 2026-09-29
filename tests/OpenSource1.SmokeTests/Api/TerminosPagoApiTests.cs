using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenSource1.Application.Features.TerminosPago.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

[Collection(PostgresCollection.Name)]
public sealed class TerminosPagoApiTests : IClassFixture<PostgresTestFixture>
{
    private readonly HttpClient _client;

    public TerminosPagoApiTests(PostgresTestFixture fixture)
    {
        var factory = fixture.CreateFactory();
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Crud_And_Authorization_Work()
    {
        var anon = new HttpRequestMessage(HttpMethod.Get, "/api/terminos-pago");
        anon.Headers.Add("X-Test-Anonymous", "true");
        var anonymousResponse = await _client.SendAsync(anon);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        var forbiddenClient = CreateClient("Supervisor");
        var forbidden = await forbiddenClient.PostAsJsonAsync(
            "/api/terminos-pago",
            new { codigo = $"F{Guid.NewGuid():N}"[..10], descripcion = "No debería crearse", diasVencimiento = 30, diasDescuento = 10, porcentajeDescuento = 2m });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var client = CreateClient("Administrador");
        var codigo = $"T{Guid.NewGuid():N}"[..15];
        var create = await client.PostAsJsonAsync(
            "/api/terminos-pago",
            new { codigo, descripcion = "Contado", diasVencimiento = 0, diasDescuento = 0, porcentajeDescuento = 0m });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var body = await create.Content.ReadAsStringAsync();
        var created = JsonDocument.Parse(body).RootElement.GetProperty("id").GetGuid();

        var list = await client.GetAsync("/api/terminos-pago");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var paged = await list.Content.ReadFromJsonAsync<PagedResult<TerminoPagoResponse>>();
        Assert.NotNull(paged);
        Assert.Contains(paged!.Items, x => x.Id == created);
        Assert.True(paged.Total >= 1);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/terminos-pago/{created}")).StatusCode);

        var update = await client.PutAsJsonAsync(
            $"/api/terminos-pago/{created}",
            new { codigo, descripcion = "30 días neto", diasVencimiento = 30, diasDescuento = 10, porcentajeDescuento = 2.5m });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<TerminoPagoResponse>();
        Assert.Equal("30 días neto", updated!.Descripcion);
        Assert.Equal(30, updated.DiasVencimiento);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/terminos-pago/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/terminos-pago/{Guid.NewGuid()}")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Supervisor").DeleteAsync($"/api/terminos-pago/{created}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Ejecutor").DeleteAsync($"/api/terminos-pago/{created}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await CreateClient("Administrador").DeleteAsync($"/api/terminos-pago/{created}")).StatusCode);
    }

    [Fact]
    public async Task Create_ConCamposInvalidos_Devuelve400ConErroresPorCampo()
    {
        var client = CreateClient("Administrador");

        var response = await client.PostAsJsonAsync(
            "/api/terminos-pago",
            new { codigo = "", descripcion = "", diasVencimiento = -1, diasDescuento = -1, porcentajeDescuento = 150m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problema = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var errores = problema!.RootElement.GetProperty("errors");
        Assert.True(errores.TryGetProperty("Codigo", out _));
        Assert.True(errores.TryGetProperty("Descripcion", out _));
        Assert.True(errores.TryGetProperty("DiasVencimiento", out _));
        Assert.True(errores.TryGetProperty("DiasDescuento", out _));
        Assert.True(errores.TryGetProperty("PorcentajeDescuento", out _));
    }

    [Fact]
    public async Task Create_ConCodigoDuplicado_Devuelve409()
    {
        var client = CreateClient("Administrador");
        var codigo = $"D{Guid.NewGuid():N}"[..15];

        var primero = await client.PostAsJsonAsync(
            "/api/terminos-pago",
            new { codigo, descripcion = "Original", diasVencimiento = 15, diasDescuento = 5, porcentajeDescuento = 1m });
        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);

        var duplicado = await client.PostAsJsonAsync(
            "/api/terminos-pago",
            new { codigo, descripcion = "Duplicado", diasVencimiento = 15, diasDescuento = 5, porcentajeDescuento = 1m });
        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);
    }

    [Fact]
    public async Task Create_TrasBorrarElMismoCodigo_NoChocaConElIndiceUnico_YDevuelve201()
    {
        // Mismo hallazgo que ProductosApiTests: el filtro global de EF (!IsDeleted) oculta la
        // fila borrada lógicamente del chequeo de existencia; sin el índice único parcial
        // "IsDeleted = false" el INSERT de abajo chocaría contra el índice a nivel de Postgres.
        var client = CreateClient("Administrador");
        var codigo = $"B{Guid.NewGuid():N}"[..15];

        var create = await client.PostAsJsonAsync(
            "/api/terminos-pago",
            new { codigo, descripcion = "Uno", diasVencimiento = 10, diasDescuento = 0, porcentajeDescuento = 0m });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var createdBody = await create.Content.ReadAsStringAsync();
        var createdId = JsonDocument.Parse(createdBody).RootElement.GetProperty("id").GetGuid();

        var delete = await client.DeleteAsync($"/api/terminos-pago/{createdId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var recreate = await client.PostAsJsonAsync(
            "/api/terminos-pago",
            new { codigo, descripcion = "Dos", diasVencimiento = 20, diasDescuento = 0, porcentajeDescuento = 0m });

        Assert.Equal(HttpStatusCode.Created, recreate.StatusCode);
        Assert.NotEqual(HttpStatusCode.InternalServerError, recreate.StatusCode);
    }

    [Fact]
    public async Task List_RespetaElTamanoDePaginaYDevuelveElTotal()
    {
        var client = CreateClient("Administrador");

        for (var i = 0; i < 3; i++)
        {
            var codigo = $"P{Guid.NewGuid():N}"[..15];
            var create = await client.PostAsJsonAsync(
                "/api/terminos-pago",
                new { codigo, descripcion = $"Pag{i}", diasVencimiento = 0, diasDescuento = 0, porcentajeDescuento = 0m });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        }

        var response = await client.GetAsync("/api/terminos-pago?tamanoPagina=2&pagina=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var paged = await response.Content.ReadFromJsonAsync<PagedResult<TerminoPagoResponse>>();
        Assert.NotNull(paged);
        Assert.Equal(2, paged!.Items.Count);
        Assert.True(paged.Total >= 3);
        Assert.True(paged.TotalPaginas >= 2);
        Assert.Equal(1, paged.Pagina);
        Assert.Equal(2, paged.TamanoPagina);
    }

    [Fact]
    public async Task List_ColumnaDeOrdenNoPermitida_CaeAlOrdenPorDefectoSinRomper()
    {
        var client = CreateClient("Administrador");

        var response = await client.GetAsync(
            "/api/terminos-pago?ordenarPor=" + Uri.EscapeDataString("\"; DROP TABLE \"TerminosPago") + "&tamanoPagina=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await response.Content.ReadFromJsonAsync<PagedResult<TerminoPagoResponse>>();
        Assert.NotNull(paged);
    }

    [Fact]
    public async Task Listado_Y_GetPorId_NoDevuelvenUnTerminoBorradoLogicamente()
    {
        // Protege el predicado "IsDeleted" = false de DapperTerminoPagoReadRepository (lista y GET por Id).
        var client = CreateClient("Administrador");
        var marca = Guid.NewGuid().ToString("N")[..8];

        var conservado = await CrearAsync(client, $"K{marca}A");
        var borrado = await CrearAsync(client, $"K{marca}B");
        Assert.Equal(2, (await ListarPorMarcaAsync(client, marca)).Total);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/terminos-pago/{borrado}")).StatusCode);

        var paged = await ListarPorMarcaAsync(client, marca);
        Assert.Equal(1, paged.Total);
        Assert.Equal(conservado, Assert.Single(paged.Items).Id);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/terminos-pago/{conservado}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/terminos-pago/{borrado}")).StatusCode);
    }

    private static async Task<Guid> CrearAsync(HttpClient client, string codigo)
    {
        var create = await client.PostAsJsonAsync(
            "/api/terminos-pago",
            new { codigo, descripcion = "Borrado logico", diasVencimiento = 0, diasDescuento = 0, porcentajeDescuento = 0m });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        return JsonDocument.Parse(await create.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<PagedResult<TerminoPagoResponse>> ListarPorMarcaAsync(HttpClient client, string marca)
    {
        var response = await client.GetAsync($"/api/terminos-pago?codigo={marca}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PagedResult<TerminoPagoResponse>>())!;
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
