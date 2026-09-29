using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenSource1.Api;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

/// <summary>
/// Task 7.5: una <c>pagina</c> enorme (<c>int.MaxValue</c>) no puede dar 500 en ningún listado paginado. Todos, incluidas
/// las vistas de la Fase 7, se apoyan en <see cref="OpenSource1.Core.Common.PageRequest.Offset"/>, acotado a
/// <c>int.MaxValue</c>: responden 200 con una página vacía que conserva la página pedida (las páginas Blazor saltan entonces
/// a la última). Los movimientos de un cliente (ruta con socio) se cubren en <c>ClientesVistasApiTests</c>. REQUIERE DOCKER.
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

    /// <summary>Las vistas de la Fase 7: mismo contrato; existencias y estado de cuenta anidan la página en <c>pagina</c>.</summary>
    [Theory]
    [InlineData("/api/inventario/movimientos-producto", false)]
    [InlineData("/api/inventario/movimientos-valor", false)]
    [InlineData("/api/inventario/existencias", true)]
    [InlineData("/api/contabilidad/movimientos", false)]
    [InlineData("/api/clientes/estado-cuenta", true)]
    public async Task VistaFase7_PaginaEnorme_Devuelve200ConPaginaVacia(string url, bool anidada)
    {
        foreach (var tamano in new[] { 50, 200 })
        {
            using var respuesta = await _client.GetAsync($"{url}?{PaginaEnorme}&tamanoPagina={tamano}");
            var cuerpo = await respuesta.Content.ReadAsStringAsync();

            Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"{url} (tamaño {tamano}): {(int)respuesta.StatusCode} {cuerpo}");
            using var json = JsonDocument.Parse(cuerpo);
            var pagina = anidada ? json.RootElement.GetProperty("pagina") : json.RootElement;
            Assert.Equal(0, pagina.GetProperty("items").GetArrayLength());
            Assert.Equal(int.MaxValue, pagina.GetProperty("pagina").GetInt32());
        }
    }
}
