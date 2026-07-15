using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

public sealed class ProductosApiTests : IClassFixture<PostgresTestFixture>
{
    private readonly HttpClient _client;

    public ProductosApiTests(PostgresTestFixture fixture)
    {
        _client = fixture.CreateFactory().CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Crud_And_Auth_Are_Wired()
    {
        var anon = new HttpRequestMessage(HttpMethod.Get, "/api/productos");
        anon.Headers.Add("X-Test-Anonymous", "true");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(anon)).StatusCode);

        var forbiddenClient = CreateClient("Supervisor");
        var forbidden = await forbiddenClient.PostAsJsonAsync("/api/productos", new { codigo = $"P-{Guid.NewGuid():N}", nombre = "Prod", categoria = "General", precio = 10.5m, stock = 5 });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var client = CreateClient("Administrador");
        var codigo = $"P-{Guid.NewGuid():N}";
        var create = await client.PostAsJsonAsync("/api/productos", new { codigo, nombre = "Prod", categoria = "General", precio = 10.5m, stock = 5 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/productos/{created}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/productos/{created}", new { codigo, nombre = "Prod 2", categoria = "General", precio = 11m, stock = 7 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/productos/{Guid.NewGuid()}")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Supervisor").DeleteAsync($"/api/productos/{created}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Ejecutor").DeleteAsync($"/api/productos/{created}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await CreateClient("Administrador").DeleteAsync($"/api/productos/{created}")).StatusCode);
    }

    private HttpClient CreateClient(string role)
    {
        _client.DefaultRequestHeaders.Remove("X-Test-Anonymous");
        _client.DefaultRequestHeaders.Remove("X-Test-User");
        _client.DefaultRequestHeaders.Remove("X-Test-Roles");
        _client.DefaultRequestHeaders.Add("X-Test-User", role.ToLowerInvariant());
        _client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return _client;
    }
}
