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

        var update = await admin.PutAsJsonAsync($"/api/users/{userId}", new { fullName = "Test User Updated" });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var searchByName = JsonDocument.Parse(await (await admin.GetAsync("/api/users?search=Updated")).Content.ReadAsStringAsync()).RootElement;
        Assert.Contains(searchByName.EnumerateArray(), x => x.GetProperty("id").GetString() == userId);

        var resetPassword = await admin.PostAsJsonAsync($"/api/users/{userId}/reset-password", new { newPassword = "NuevaPassword123" });
        Assert.Equal(HttpStatusCode.NoContent, resetPassword.StatusCode);

        var toggle = await admin.PostAsJsonAsync($"/api/users/{userId}/toggle-active", new { });
        Assert.Equal(HttpStatusCode.NoContent, toggle.StatusCode);

        var delete = await admin.DeleteAsync($"/api/users/{userId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Theory]
    [InlineData("Supervisor")]
    [InlineData("Ejecutor")]
    public async Task NonAdmin_Cannot_Access_Users(string role)
    {
        var client = CreateClient(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/users", new { email = $"blocked-{Guid.NewGuid():N}@test.local", fullName = "Blocked", password = "Password123" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/users/{Guid.NewGuid()}")).StatusCode);
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

    private HttpClient CreateClient(string role = "Administrador")
    {
        _client.DefaultRequestHeaders.Remove("X-Test-Anonymous");
        _client.DefaultRequestHeaders.Remove("X-Test-User");
        _client.DefaultRequestHeaders.Remove("X-Test-Roles");
        _client.DefaultRequestHeaders.Add("X-Test-User", role.ToLowerInvariant());
        _client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return _client;
    }
}
