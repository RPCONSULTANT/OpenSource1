using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

/// <summary>
/// Task 8.2: los módulos de prueba obsoletos (entradas y configuración clave/valor) se retiraron por completo; sus rutas de la
/// API ya no existen y responden 404 a un usuario autenticado con todos los permisos.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ModulosRetiradosApiTests : IClassFixture<PostgresTestFixture>
{
    private readonly HttpClient _client;

    public ModulosRetiradosApiTests(PostgresTestFixture fixture)
    {
        _client = fixture.CreateFactory().CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Theory]
    [InlineData("/api/entradas")]
    [InlineData("/api/app-settings")]
    public async Task RutasRetiradas_Devuelven404(string url)
    {
        using var listado = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.NotFound, listado.StatusCode);

        using var porClave = await _client.GetAsync($"{url}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, porClave.StatusCode);

        using var alta = await _client.PostAsJsonAsync(url, new { key = "x", value = "y", titulo = "t", tipo = "t", estado = "e" });
        Assert.Equal(HttpStatusCode.NotFound, alta.StatusCode);
    }
}
