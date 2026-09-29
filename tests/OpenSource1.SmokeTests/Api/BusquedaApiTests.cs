using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using OpenSource1.Application.Features.Busqueda.Dtos;
using OpenSource1.SmokeTests.TestInfrastructure;
using static OpenSource1.SmokeTests.TestInfrastructure.LibroClientesSemilla;

namespace OpenSource1.SmokeTests.Api;

/// <summary>
/// Fix-Features A3: GET api/busqueda contra Postgres real. Cada test usa un marcador único para no depender de los datos de
/// otros tests de la colección. Las notas de crédito (posteadas y borradores) se ejercitan por ejecución de su SQL (grupos
/// presentes y vacíos para un marcador que no existe). REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class BusquedaApiTests : IClassFixture<PostgresTestFixture>
{
    private readonly PostgresTestFixture _fixture;
    private readonly HttpClient _client;

    public BusquedaApiTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateFactory().CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Encuentra_Socios_Productos_Facturas_YBorradores_ConSusRutas()
    {
        var marcador = Marcador();
        var client = Rol("Administrador");
        await using var conexion = await AbrirAsync();
        var socio = await InsertarSocioAsync(conexion, $"Comercial {marcador}");
        var producto = await CrearProductoAsync(client, $"Tornillo {marcador}");
        var numero = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        await InsertarFacturaAsync(conexion, numero, socio, new DateOnly(2026, 9, 10), [], [new LineaIva("EXENTO", 0m, 1m, 0m)], null, $"Cliente {marcador}");
        var borrador = await client.PostAsJsonAsync("/api/facturas-venta/borradores", new { socioNegocioId = socio });
        Assert.True(borrador.StatusCode == HttpStatusCode.Created, await borrador.Content.ReadAsStringAsync());

        var resultado = await BuscarAsync(client, marcador);

        Assert.Equal(
            [TiposResultadoBusqueda.Clientes, TiposResultadoBusqueda.Productos, TiposResultadoBusqueda.Facturas,
             TiposResultadoBusqueda.BorradoresFactura, TiposResultadoBusqueda.NotasCredito, TiposResultadoBusqueda.BorradoresNotaCredito],
            resultado.Grupos.Select(g => g.Tipo));
        var cliente = Assert.Single(Grupo(resultado, TiposResultadoBusqueda.Clientes));
        Assert.Equal((socio.ToString(), $"/clientes/{socio}"), (cliente.Id, cliente.Ruta));
        var productoItem = Assert.Single(Grupo(resultado, TiposResultadoBusqueda.Productos));
        Assert.Equal($"/productos/{producto}", productoItem.Ruta);
        var factura = Assert.Single(Grupo(resultado, TiposResultadoBusqueda.Facturas));
        Assert.Equal((numero, $"/facturas-venta/{numero}"), (factura.Titulo, factura.Ruta));
        var borradorItem = Assert.Single(Grupo(resultado, TiposResultadoBusqueda.BorradoresFactura));
        Assert.StartsWith("/facturas-venta/borradores/", borradorItem.Ruta);
        Assert.Empty(Grupo(resultado, TiposResultadoBusqueda.NotasCredito));
        Assert.Empty(Grupo(resultado, TiposResultadoBusqueda.BorradoresNotaCredito));
    }

    [Fact]
    public async Task LimitePorTipo_YExcluyeBorradosLogicos()
    {
        var marcador = Marcador();
        await using var conexion = await AbrirAsync();
        await InsertarSocioAsync(conexion, $"Uno {marcador}");
        await InsertarSocioAsync(conexion, $"Dos {marcador}");
        var borrado = await InsertarSocioAsync(conexion, $"Tres {marcador}");
        await conexion.ExecuteAsync("UPDATE \"SociosNegocio\" SET \"IsDeleted\" = true WHERE \"Id\" = @Id", new { Id = borrado });

        var todos = await BuscarAsync(Rol("Administrador"), marcador);
        var limitado = await BuscarAsync(Rol("Administrador"), marcador, "&limite=1");

        Assert.Equal(2, Grupo(todos, TiposResultadoBusqueda.Clientes).Count);
        Assert.DoesNotContain(Grupo(todos, TiposResultadoBusqueda.Clientes), r => r.Id == borrado.ToString());
        Assert.Single(Grupo(limitado, TiposResultadoBusqueda.Clientes));
    }

    [Fact]
    public async Task Metacaracteres_SeBuscanLiteralmente()
    {
        var marcador = Marcador();
        await using var conexion = await AbrirAsync();
        var literal = await InsertarSocioAsync(conexion, $"Cien%_\\{marcador}");
        await InsertarSocioAsync(conexion, $"CienXY{marcador}");

        var resultado = await BuscarAsync(Rol("Administrador"), $"%_\\{marcador}");

        var unico = Assert.Single(Grupo(resultado, TiposResultadoBusqueda.Clientes));
        Assert.Equal(literal.ToString(), unico.Id);
        Assert.Empty(Grupo(await BuscarAsync(Rol("Administrador"), $"'{marcador}"), TiposResultadoBusqueda.Clientes));
    }

    [Fact]
    public async Task Validacion_Q_Limite_YPermisos()
    {
        var client = Rol("Administrador");
        await AssertErrorAsync(await client.GetAsync("/api/busqueda?q=a"), "q");
        await AssertErrorAsync(await client.GetAsync("/api/busqueda?q=%20%20a%20%20"), "q");
        await AssertErrorAsync(await client.GetAsync($"/api/busqueda?q={new string('x', 101)}"), "q");
        await AssertErrorAsync(await client.GetAsync("/api/busqueda"), "q");
        await AssertErrorAsync(await client.GetAsync("/api/busqueda?q=abc&limite=0"), "limite");
        await AssertErrorAsync(await client.GetAsync("/api/busqueda?q=abc&limite=21"), "limite");

        Assert.Equal(HttpStatusCode.OK, (await Rol("Ejecutor").GetAsync("/api/busqueda?q=abc")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Rol("Supervisor").GetAsync("/api/busqueda?q=abc")).StatusCode);

        var anonimo = new HttpRequestMessage(HttpMethod.Get, "/api/busqueda?q=abc");
        anonimo.Headers.Add("X-Test-Anonymous", "true");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(anonimo)).StatusCode);
    }

    private static string Marcador() => "Zq" + Guid.NewGuid().ToString("N")[..10];

    private static IReadOnlyList<ResultadoBusqueda> Grupo(BusquedaGlobalResponse respuesta, string tipo) =>
        respuesta.Grupos.Single(g => g.Tipo == tipo).Items;

    private static async Task<BusquedaGlobalResponse> BuscarAsync(HttpClient client, string q, string extra = "")
    {
        var response = await client.GetAsync($"/api/busqueda?q={Uri.EscapeDataString(q)}{extra}");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<BusquedaGlobalResponse>())!;
    }

    private static async Task<Guid> CrearProductoAsync(HttpClient client, string nombre)
    {
        var response = await client.PostAsJsonAsync(
            "/api/productos", new { codigo = $"BG{Guid.NewGuid():N}"[..11], nombre, precioVenta = 10.03m });
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, string campo)
    {
        var cuerpo = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"Se esperaba 400 y llegó {response.StatusCode}: {cuerpo}");
        Assert.True(JsonDocument.Parse(cuerpo).RootElement.GetProperty("errors").TryGetProperty(campo, out _), cuerpo);
    }

    private async Task<NpgsqlConnection> AbrirAsync()
    {
        var conexion = new NpgsqlConnection(_fixture.AppConnectionString);
        await conexion.OpenAsync();
        return conexion;
    }

    private HttpClient Rol(string role)
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
