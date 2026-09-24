using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenSource1.Application.Features.UnidadesMedida.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

[Collection(PostgresCollection.Name)]
public sealed class UnidadesMedidaApiTests : IClassFixture<PostgresTestFixture>
{
    private readonly HttpClient _client;

    public UnidadesMedidaApiTests(PostgresTestFixture fixture)
    {
        var factory = fixture.CreateFactory();
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Crud_And_Authorization_Work()
    {
        var anon = new HttpRequestMessage(HttpMethod.Get, "/api/unidades-medida");
        anon.Headers.Add("X-Test-Anonymous", "true");
        var anonymousResponse = await _client.SendAsync(anon);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        var forbiddenClient = CreateClient("Supervisor");
        var forbidden = await forbiddenClient.PostAsJsonAsync(
            "/api/unidades-medida",
            new { codigo = $"F{Guid.NewGuid():N}"[..10], nombre = "No debería crearse", decimales = 0 });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var client = CreateClient("Administrador");
        var codigo = $"T{Guid.NewGuid():N}"[..10];
        var create = await client.PostAsJsonAsync(
            "/api/unidades-medida",
            new { codigo, nombre = "Contado", decimales = 0 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var body = await create.Content.ReadAsStringAsync();
        var created = JsonDocument.Parse(body).RootElement.GetProperty("id").GetGuid();

        var list = await client.GetAsync("/api/unidades-medida");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var paged = await list.Content.ReadFromJsonAsync<PagedResult<UnidadMedidaResponse>>();
        Assert.NotNull(paged);
        Assert.Contains(paged!.Items, x => x.Id == created);
        Assert.True(paged.Total >= 1);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/unidades-medida/{created}")).StatusCode);

        var update = await client.PutAsJsonAsync(
            $"/api/unidades-medida/{created}",
            new { codigo, nombre = "Cajita", decimales = 2 });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<UnidadMedidaResponse>();
        Assert.Equal("Cajita", updated!.Nombre);
        Assert.Equal(2, updated.Decimales);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/unidades-medida/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/unidades-medida/{Guid.NewGuid()}")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Supervisor").DeleteAsync($"/api/unidades-medida/{created}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Ejecutor").DeleteAsync($"/api/unidades-medida/{created}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await CreateClient("Administrador").DeleteAsync($"/api/unidades-medida/{created}")).StatusCode);
    }

    [Fact]
    public async Task Create_ConCamposInvalidos_Devuelve400ConErroresPorCampo()
    {
        var client = CreateClient("Administrador");

        var response = await client.PostAsJsonAsync(
            "/api/unidades-medida",
            new { codigo = "", nombre = "", decimales = 9 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problema = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var errores = problema!.RootElement.GetProperty("errors");
        Assert.True(errores.TryGetProperty("Codigo", out _));
        Assert.True(errores.TryGetProperty("Nombre", out _));
        Assert.True(errores.TryGetProperty("Decimales", out _));
    }

    [Fact]
    public async Task Create_ConCodigoDuplicado_Devuelve409()
    {
        var client = CreateClient("Administrador");
        var codigo = $"D{Guid.NewGuid():N}"[..10];

        var primero = await client.PostAsJsonAsync(
            "/api/unidades-medida",
            new { codigo, nombre = "Original", decimales = 0 });
        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);

        var duplicado = await client.PostAsJsonAsync(
            "/api/unidades-medida",
            new { codigo, nombre = "Duplicado", decimales = 0 });
        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);
    }

    [Fact]
    public async Task Create_TrasBorrarElMismoCodigo_NoChocaConElIndiceUnico_YDevuelve201()
    {
        // Mismo hallazgo que AppSettingsApiTests: el filtro global de EF (!IsDeleted) oculta la
        // fila borrada lógicamente del chequeo de existencia; sin el índice único parcial
        // "IsDeleted = false" el INSERT de abajo chocaría contra el índice a nivel de Postgres.
        var client = CreateClient("Administrador");
        var codigo = $"B{Guid.NewGuid():N}"[..10];

        var create = await client.PostAsJsonAsync(
            "/api/unidades-medida",
            new { codigo, nombre = "Uno", decimales = 0 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var createdBody = await create.Content.ReadAsStringAsync();
        var createdId = JsonDocument.Parse(createdBody).RootElement.GetProperty("id").GetGuid();

        var delete = await client.DeleteAsync($"/api/unidades-medida/{createdId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var recreate = await client.PostAsJsonAsync(
            "/api/unidades-medida",
            new { codigo, nombre = "Dos", decimales = 0 });

        Assert.Equal(HttpStatusCode.Created, recreate.StatusCode);
        Assert.NotEqual(HttpStatusCode.InternalServerError, recreate.StatusCode);
    }

    [Fact]
    public async Task List_RespetaElTamanoDePaginaYDevuelveElTotal()
    {
        var client = CreateClient("Administrador");

        for (var i = 0; i < 3; i++)
        {
            var codigo = $"P{Guid.NewGuid():N}"[..10];
            var create = await client.PostAsJsonAsync(
                "/api/unidades-medida",
                new { codigo, nombre = $"Pag{i}", decimales = 0 });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        }

        var response = await client.GetAsync("/api/unidades-medida?tamanoPagina=2&pagina=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var paged = await response.Content.ReadFromJsonAsync<PagedResult<UnidadMedidaResponse>>();
        Assert.NotNull(paged);
        Assert.Equal(2, paged!.Items.Count);
        Assert.True(paged.Total >= 3);
        Assert.True(paged.TotalPaginas >= 2);
        Assert.Equal(1, paged.Pagina);
        Assert.Equal(2, paged.TamanoPagina);
    }

    [Fact]
    public async Task List_IncluyeElCatalogoSembradoPorLaMigracion()
    {
        var client = CreateClient("Administrador");

        var response = await client.GetAsync("/api/unidades-medida?tamanoPagina=50&ordenarPor=Codigo&descendente=false");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var paged = await response.Content.ReadFromJsonAsync<PagedResult<UnidadMedidaResponse>>();
        var codigos = paged!.Items.Select(x => x.Codigo).ToList();
        foreach (var esperado in new[] { "UND", "KG", "GR", "LT", "ML", "CJA", "DOC", "PAQ", "MT", "LB" })
        {
            Assert.Contains(esperado, codigos);
        }
    }

    [Fact]
    public async Task Create_NormalizaElCodigoAMayusculas()
    {
        var client = CreateClient("Administrador");
        var codigo = $"q{Guid.NewGuid():N}"[..8];

        var create = await client.PostAsJsonAsync("/api/unidades-medida", new { codigo, nombre = "Quintal", decimales = 2 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var creada = await create.Content.ReadFromJsonAsync<UnidadMedidaResponse>();
        Assert.Equal(codigo.ToUpperInvariant(), creada!.Codigo);
    }

    [Fact]
    public async Task List_ColumnaDeOrdenNoPermitida_CaeAlOrdenPorDefectoSinRomper()
    {
        var client = CreateClient("Administrador");

        var response = await client.GetAsync(
            "/api/unidades-medida?ordenarPor=" + Uri.EscapeDataString("\"; DROP TABLE \"UnidadesMedida") + "&tamanoPagina=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await response.Content.ReadFromJsonAsync<PagedResult<UnidadMedidaResponse>>();
        Assert.NotNull(paged);
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
