using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

[Collection(PostgresCollection.Name)]
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
        var forbidden = await forbiddenClient.PostAsJsonAsync("/api/productos", new { codigo = $"P-{Guid.NewGuid():N}", nombre = "Prod", categoriaCodigo = "GEN", categoriaNombre = "General", unidadMedidaCodigo = "UND", precio = 10.5m, stock = 5 });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var client = CreateClient("Administrador");
        var codigo = $"P-{Guid.NewGuid():N}";
        var create = await client.PostAsJsonAsync("/api/productos", new { codigo, nombre = "Prod", categoriaCodigo = "GEN", categoriaNombre = "General", unidadMedidaCodigo = "UND", precio = 10.5m, stock = 5 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        var list = await client.GetAsync("/api/productos");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var paged = await list.Content.ReadFromJsonAsync<PagedResult<ProductoResponse>>();
        Assert.NotNull(paged);
        Assert.Contains(paged!.Items, p => p.Id == created);
        Assert.True(paged.Total >= 1);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/productos/{created}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/productos/{created}", new { codigo, nombre = "Prod 2", categoriaCodigo = "GEN", categoriaNombre = "General", unidadMedidaCodigo = "UND", precio = 11m, stock = 7 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/productos/{Guid.NewGuid()}")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Supervisor").DeleteAsync($"/api/productos/{created}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Ejecutor").DeleteAsync($"/api/productos/{created}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await CreateClient("Administrador").DeleteAsync($"/api/productos/{created}")).StatusCode);
    }

    [Fact]
    public async Task List_RespetaElTamanoDePaginaYDevuelveElTotal()
    {
        var client = CreateClient("Administrador");

        for (var i = 0; i < 3; i++)
        {
            var codigo = $"PAG-{Guid.NewGuid():N}";
            var create = await client.PostAsJsonAsync("/api/productos", new { codigo, nombre = $"ProdPag{i}", categoriaCodigo = "GEN", categoriaNombre = "General", unidadMedidaCodigo = "UND", precio = 1m, stock = 1 });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        }

        var response = await client.GetAsync("/api/productos?tamanoPagina=2&pagina=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var paged = await response.Content.ReadFromJsonAsync<PagedResult<ProductoResponse>>();
        Assert.NotNull(paged);
        Assert.Equal(2, paged!.Items.Count);
        Assert.True(paged.Total >= 3);
        Assert.True(paged.TotalPaginas >= 2);
    }

    [Fact]
    public async Task List_FiltroDePrecioInvalido_Devuelve400SinLanzar()
    {
        var client = CreateClient("Administrador");

        var response = await client.GetAsync("/api/productos?precio=no-es-un-numero");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_TrasBorrarElMismoCodigo_NoChocaConElIndiceUnico_YDevuelve201()
    {
        // Hallazgo 1, mismo mecanismo que AppSettings.Key pero en un módulo vivo: verificado
        // contra Postgres real que borrar (soft delete) y recrear con el mismo Codigo ya no
        // choca contra "IX_Productos_Codigo" gracias al índice único parcial "IsDeleted = false".
        var client = CreateClient("Administrador");
        var codigo = $"DEL-{Guid.NewGuid():N}";

        var create = await client.PostAsJsonAsync("/api/productos", new { codigo, nombre = "Original", categoriaCodigo = "GEN", categoriaNombre = "General", unidadMedidaCodigo = "UND", precio = 10m, stock = 1 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var createdId = JsonDocument.Parse(await create.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        var delete = await client.DeleteAsync($"/api/productos/{createdId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var recreate = await client.PostAsJsonAsync("/api/productos", new { codigo, nombre = "Recreado", categoriaCodigo = "GEN", categoriaNombre = "General", unidadMedidaCodigo = "UND", precio = 12m, stock = 2 });

        Assert.Equal(HttpStatusCode.Created, recreate.StatusCode);
        Assert.NotEqual(HttpStatusCode.InternalServerError, recreate.StatusCode);
    }

    [Fact]
    public async Task Create_ConCodigoDuplicadoActivo_Devuelve409EnVezDe500()
    {
        // Hallazgo 1, parte 2: ProductosController.Create no hace un chequeo de existencia previo
        // (a diferencia de AppSettingsController), así que un Codigo duplicado siempre llegó hasta
        // el INSERT y violaba "IX_Productos_Codigo" directamente. Antes del fix a
        // GlobalExceptionHandler, el DbUpdateException resultante (con Npgsql.PostgresException
        // SqlState 23505 como InnerException, confirmado contra Postgres real) no tenía rama
        // dedicada y cae como 500 desnudo. Con el fix, se traduce a 409.
        var client = CreateClient("Administrador");
        var codigo = $"DUP-{Guid.NewGuid():N}";

        var primero = await client.PostAsJsonAsync("/api/productos", new { codigo, nombre = "Primero", categoriaCodigo = "GEN", categoriaNombre = "General", unidadMedidaCodigo = "UND", precio = 10m, stock = 1 });
        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);

        var duplicado = await client.PostAsJsonAsync("/api/productos", new { codigo, nombre = "Segundo", categoriaCodigo = "GEN", categoriaNombre = "General", unidadMedidaCodigo = "UND", precio = 10m, stock = 1 });

        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);
        Assert.NotEqual(HttpStatusCode.InternalServerError, duplicado.StatusCode);

        var cuerpo = await duplicado.Content.ReadAsStringAsync();
        Assert.Contains("entidad.codigo_duplicado", cuerpo);
    }

    [Fact]
    public async Task List_ColumnaDeOrdenNoPermitida_CaeAlOrdenPorDefectoSinRomper()
    {
        var client = CreateClient("Administrador");

        var response = await client.GetAsync("/api/productos?ordenarPor=" + Uri.EscapeDataString("\"; DROP TABLE \"Productos") + "&tamanoPagina=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await response.Content.ReadFromJsonAsync<PagedResult<ProductoResponse>>();
        Assert.NotNull(paged);
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
