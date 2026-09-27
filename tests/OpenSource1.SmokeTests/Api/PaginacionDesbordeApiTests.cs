using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenSource1.Api;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

/// <summary>
/// Task 7.5: una <c>pagina</c> enorme (<c>int.MaxValue</c>) no puede dar 500 en ningún listado paginado. Los listados
/// anteriores a la Fase 7 (sin <c>PaginacionValidacion</c>) se apoyan en <see cref="OpenSource1.Core.Common.PageRequest.Offset"/>,
/// acotado a <c>int.MaxValue</c>: responden 200 con una página vacía que conserva la página pedida. Las vistas de la Fase 7
/// siguen respondiendo 400 con <c>Campo = "Pagina"</c> (cubierto en sus propios tests). REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PaginacionDesbordeApiTests : IClassFixture<PostgresTestFixture>, IDisposable
{
    private const string PaginaEnorme = "pagina=2147483647";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public PaginacionDesbordeApiTests(PostgresTestFixture fixture)
    {
        _factory = fixture.CreateFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        _client.DefaultRequestHeaders.Add("X-Test-User", "administrador");
        _client.DefaultRequestHeaders.Add("X-Test-Roles", "Administrador");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Theory]
    [InlineData("/api/productos")]
    [InlineData("/api/socios-negocio")]
    [InlineData("/api/almacenes")]
    [InlineData("/api/facturas-venta")]
    [InlineData("/api/facturas-venta/borradores")]
    [InlineData("/api/categorias-producto")]
    [InlineData("/api/unidades-medida")]
    [InlineData("/api/terminos-pago")]
    [InlineData("/api/cuentas-contables")]
    [InlineData("/api/grupos-contables/negocio")]
    [InlineData("/api/grupos-cliente-contable")]
    [InlineData("/api/setups-contables/general")]
    [InlineData("/api/setups-contables/iva")]
    [InlineData("/api/setups-contables/inventario")]
    [InlineData("/api/diarios-inventario/lotes")]
    [InlineData("/api/diarios-inventario/registros")]
    [InlineData("/api/contabilidad/registros")]
    [InlineData("/api/app-settings")]
    [InlineData("/api/entradas")]
    public async Task ListadoAntiguo_PaginaEnorme_Devuelve200ConPaginaVacia(string url)
    {
        foreach (var tamano in new[] { 50, 200 })
        {
            using var respuesta = await _client.GetAsync($"{url}?{PaginaEnorme}&tamanoPagina={tamano}");
            var cuerpo = await respuesta.Content.ReadAsStringAsync();

            Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"{url} (tamaño {tamano}): {(int)respuesta.StatusCode} {cuerpo}");
            using var json = JsonDocument.Parse(cuerpo);
            Assert.Equal(0, json.RootElement.GetProperty("items").GetArrayLength());
            Assert.Equal(int.MaxValue, json.RootElement.GetProperty("pagina").GetInt32());
        }
    }

    [Theory]
    [InlineData("/api/inventario/movimientos-producto")]
    [InlineData("/api/inventario/movimientos-valor")]
    [InlineData("/api/inventario/existencias")]
    [InlineData("/api/contabilidad/movimientos")]
    [InlineData("/api/clientes/estado-cuenta")]
    public async Task VistaFase7_PaginaEnorme_Devuelve400ConCampoPagina(string url)
    {
        using var respuesta = await _client.GetAsync($"{url}?{PaginaEnorme}");
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.True(respuesta.StatusCode == HttpStatusCode.BadRequest, $"{url}: {(int)respuesta.StatusCode} {cuerpo}");
        using var json = JsonDocument.Parse(cuerpo);
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("Pagina", out _), cuerpo);
    }
}
