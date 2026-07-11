using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenSource1.Api;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

public sealed class AppSettingsApiTests : IClassFixture<PostgresTestFixture>
{
    private readonly HttpClient _client;

    public AppSettingsApiTests(PostgresTestFixture fixture)
    {
        var factory = fixture.CreateFactory();
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task FullCrud_Works_Against_RealApi()
    {
        var key = $"test.setting.{Guid.NewGuid():N}";

        var create = await _client.PostAsJsonAsync("/api/app-settings", new { key, value = "one", description = "desc" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var list = await _client.GetAsync("/api/app-settings");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var get = await _client.GetAsync($"/api/app-settings/{key}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var update = await _client.PutAsJsonAsync($"/api/app-settings/{key}", new { value = "two", description = "updated" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var duplicate = await _client.PostAsJsonAsync("/api/app-settings", new { key, value = "three", description = "dup" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var invalid = await _client.PostAsJsonAsync("/api/app-settings", new { key, value = "" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var missing = await _client.GetAsync("/api/app-settings/missing-key");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var delete = await _client.DeleteAsync($"/api/app-settings/{key}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }
}
