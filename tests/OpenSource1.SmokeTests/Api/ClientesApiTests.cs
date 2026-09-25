using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenSource1.Application.Features.Clientes.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

[Collection(PostgresCollection.Name)]
public sealed class ClientesApiTests : IClassFixture<PostgresTestFixture>
{
    private readonly HttpClient _client;

    public ClientesApiTests(PostgresTestFixture fixture)
    {
        _client = fixture.CreateFactory().CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Crud_And_Authorization_Work()
    {
        var anon = new HttpRequestMessage(HttpMethod.Get, "/api/clientes");
        anon.Headers.Add("X-Test-Anonymous", "true");
        var anonymousResponse = await _client.SendAsync(anon);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        var forbiddenClient = CreateClient("Supervisor");
        var forbidden = await forbiddenClient.PostAsJsonAsync("/api/clientes", new { nombre = "A", apellido = "B", email = "a@test.local", telefono = "", direccionLinea1 = "" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var client = CreateClient("Administrador");
        var email = $"juan-{Guid.NewGuid():N}@test.local";
        var create = await client.PostAsJsonAsync("/api/clientes", new { nombre = "Juan", apellido = "Perez", email, telefono = "809-000-0000", direccionLinea1 = "Calle 1" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var body = await create.Content.ReadAsStringAsync();
        var created = JsonDocument.Parse(body).RootElement.GetProperty("id").GetGuid();

        var list = await client.GetAsync("/api/clientes");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var paged = await list.Content.ReadFromJsonAsync<PagedResult<ClienteResponse>>();
        Assert.NotNull(paged);
        Assert.Contains(paged!.Items, c => c.Id == created);
        Assert.True(paged.Total >= 1);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/clientes/{created}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/clientes/{created}", new { nombre = "Juan", apellido = "Perez", email, telefono = "809-111-1111", direccionLinea1 = "Calle 2" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/clientes/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/clientes/{Guid.NewGuid()}")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Supervisor").DeleteAsync($"/api/clientes/{created}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Ejecutor").DeleteAsync($"/api/clientes/{created}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await CreateClient("Administrador").DeleteAsync($"/api/clientes/{created}")).StatusCode);
    }

    [Fact]
    public async Task List_RespetaElTamanoDePaginaYDevuelveElTotal()
    {
        var client = CreateClient("Administrador");

        for (var i = 0; i < 3; i++)
        {
            var email = $"pag-{Guid.NewGuid():N}@test.local";
            var create = await client.PostAsJsonAsync("/api/clientes", new { nombre = "Pag", apellido = $"Cliente{i}", email, telefono = "", direccionLinea1 = "" });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        }

        var response = await client.GetAsync("/api/clientes?tamanoPagina=2&pagina=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var paged = await response.Content.ReadFromJsonAsync<PagedResult<ClienteResponse>>();
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

        var response = await client.GetAsync("/api/clientes?ordenarPor=" + Uri.EscapeDataString("\"; DROP TABLE \"Clientes") + "&tamanoPagina=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await response.Content.ReadFromJsonAsync<PagedResult<ClienteResponse>>();
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
