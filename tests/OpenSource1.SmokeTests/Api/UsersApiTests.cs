using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

public sealed class UsersApiTests : IClassFixture<PostgresTestFixture>
{
    private readonly HttpClient _client;

    public UsersApiTests(PostgresTestFixture fixture)
    {
        _client = fixture.CreateFactory().CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Admin_User_Flows_Work()
    {
        var admin = CreateClient();
        var list = await admin.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var email = $"user-{Guid.NewGuid():N}@test.local";
        var create = await admin.PostAsJsonAsync("/api/users", new { email, fullName = "Test User", password = "Password123" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var users = JsonDocument.Parse(await (await admin.GetAsync("/api/users")).Content.ReadAsStringAsync()).RootElement;
        var userId = users.EnumerateArray().First(x => x.GetProperty("email").GetString() == email).GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(userId));

        var detail = await admin.GetAsync($"/api/users/{userId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);

        var assign = await admin.PostAsJsonAsync("/api/users/assign-role", new { userId, role = "Supervisor" });
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);

        var remove = await admin.PostAsJsonAsync("/api/users/remove-role", new { userId, role = "Supervisor" });
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        var toggle = await admin.PostAsJsonAsync($"/api/users/{userId}/toggle-active", new { });
        Assert.Equal(HttpStatusCode.NoContent, toggle.StatusCode);

        var delete = await admin.DeleteAsync($"/api/users/{userId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Admin_Protections_Are_Enforced()
    {
        var admin = CreateClient();
        var users = JsonDocument.Parse(await (await admin.GetAsync("/api/users")).Content.ReadAsStringAsync()).RootElement;
        var adminId = users.EnumerateArray().First(x => x.GetProperty("email").GetString() == "admin@opensource1.local").GetProperty("id").GetString();

        var delete = await admin.DeleteAsync($"/api/users/{adminId}");
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);

        var toggle = await admin.PostAsJsonAsync($"/api/users/{adminId}/toggle-active", new { });
        Assert.Equal(HttpStatusCode.BadRequest, toggle.StatusCode);
    }

    private HttpClient CreateClient()
    {
        _client.DefaultRequestHeaders.Remove("X-Test-Anonymous");
        _client.DefaultRequestHeaders.Remove("X-Test-User");
        _client.DefaultRequestHeaders.Remove("X-Test-Roles");
        _client.DefaultRequestHeaders.Add("X-Test-User", "admin");
        _client.DefaultRequestHeaders.Add("X-Test-Roles", "Administrador");
        return _client;
    }
}
