using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using OpenSource1.Core.Entities;
using OpenSource1.Infrastructure.Data;
using OpenSource1.Application.Features.CategoriasProducto.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

[Collection(PostgresCollection.Name)]
public sealed class CategoriasProductoApiTests : IClassFixture<PostgresTestFixture>
{
    private const string Ruta = "/api/categorias-producto";
    private readonly PostgresTestFixture _fixture;
    private readonly HttpClient _client;

    public CategoriasProductoApiTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
        var factory = fixture.CreateFactory();
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Crud_And_Authorization_Work()
    {
        var anon = new HttpRequestMessage(HttpMethod.Get, Ruta);
        anon.Headers.Add("X-Test-Anonymous", "true");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(anon)).StatusCode);

        var forbidden = await CreateClient("Supervisor").PostAsJsonAsync(
            Ruta, new { codigo = CodigoUnico("F"), nombre = "No debería crearse" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var client = CreateClient("Administrador");
        var codigo = CodigoUnico("T");
        var create = await client.PostAsJsonAsync(Ruta, new { codigo, nombre = "Bebidas" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<CategoriaProductoResponse>())!;
        Assert.Null(created.CategoriaPadreId);

        var list = await client.GetAsync(Ruta);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var paged = await list.Content.ReadFromJsonAsync<PagedResult<CategoriaProductoResponse>>();
        Assert.Contains(paged!.Items, x => x.Id == created.Id);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Ruta}/{created.Id}")).StatusCode);

        var update = await client.PutAsJsonAsync($"{Ruta}/{created.Id}", new { codigo, nombre = "Bebidas frías" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Bebidas frías", (await update.Content.ReadFromJsonAsync<CategoriaProductoResponse>())!.Nombre);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Ruta}/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"{Ruta}/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Supervisor").DeleteAsync($"{Ruta}/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Ejecutor").DeleteAsync($"{Ruta}/{created.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await CreateClient("Administrador").DeleteAsync($"{Ruta}/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Ruta}/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Create_ConCamposInvalidos_Devuelve400ConErroresPorCampo()
    {
        var response = await CreateClient("Administrador").PostAsJsonAsync(Ruta, new { codigo = "", nombre = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errores = (await response.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("errors");
        Assert.True(errores.TryGetProperty("Codigo", out _));
        Assert.True(errores.TryGetProperty("Nombre", out _));
    }

    [Fact]
    public async Task Create_ConCodigoDuplicado_Devuelve409()
    {
        var client = CreateClient("Administrador");
        var codigo = CodigoUnico("D");

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Ruta, new { codigo, nombre = "Original" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Ruta, new { codigo, nombre = "Duplicado" })).StatusCode);
    }

    [Fact]
    public async Task Update_ACodigoDuplicado_Devuelve409()
    {
        var client = CreateClient("Administrador");
        var codigoA = CodigoUnico("A");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Ruta, new { codigo = codigoA, nombre = "Uno" })).StatusCode);
        var b = await Crear(client, CodigoUnico("B"));

        var update = await client.PutAsJsonAsync($"{Ruta}/{b.Id}", new { codigo = codigoA, nombre = "Dos" });

        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
    }

    [Fact]
    public async Task Create_TrasBorrarElMismoCodigo_NoChocaConElIndiceUnico_YDevuelve201()
    {
        var client = CreateClient("Administrador");
        var codigo = CodigoUnico("R");
        var creada = await Crear(client, codigo);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Ruta}/{creada.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Ruta, new { codigo, nombre = "Dos" })).StatusCode);
    }

    [Fact]
    public async Task Create_ConPadre_DevuelveElNombreDelPadre_YElListadoTambien()
    {
        var client = CreateClient("Administrador");
        var padre = await Crear(client, CodigoUnico("P"), "Alimentos");

        var hija = await Crear(client, CodigoUnico("H"), "Lácteos", padre.Id);

        Assert.Equal(padre.Id, hija.CategoriaPadreId);
        Assert.Equal("Alimentos", hija.CategoriaPadreNombre);

        var porId = await client.GetFromJsonAsync<CategoriaProductoResponse>($"{Ruta}/{hija.Id}");
        Assert.Equal("Alimentos", porId!.CategoriaPadreNombre);

        var listado = await client.GetFromJsonAsync<PagedResult<CategoriaProductoResponse>>($"{Ruta}?tamanoPagina=200");
        Assert.Equal("Alimentos", Assert.Single(listado!.Items, x => x.Id == hija.Id).CategoriaPadreNombre);
        Assert.Null(Assert.Single(listado.Items, x => x.Id == padre.Id).CategoriaPadreNombre);
    }

    [Fact]
    public async Task Create_ConPadreInexistente_Devuelve400()
    {
        var response = await CreateClient("Administrador").PostAsJsonAsync(
            Ruta, new { codigo = CodigoUnico("X"), nombre = "Huérfana", categoriaPadreId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errores = (await response.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("errors");
        Assert.True(errores.TryGetProperty("CategoriaPadreId", out _));
    }

    [Fact]
    public async Task Create_ConPadreBorrado_Devuelve400()
    {
        var client = CreateClient("Administrador");
        var padre = await Crear(client, CodigoUnico("B"));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Ruta}/{padre.Id}")).StatusCode);

        var response = await client.PostAsJsonAsync(
            Ruta, new { codigo = CodigoUnico("Y"), nombre = "Hija de borrada", categoriaPadreId = padre.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_ConPadreBorrado_Devuelve400()
    {
        var client = CreateClient("Administrador");
        var padre = await Crear(client, CodigoUnico("UB"));
        var otra = await Crear(client, CodigoUnico("UO"));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Ruta}/{padre.Id}")).StatusCode);

        var response = await client.PutAsJsonAsync($"{Ruta}/{otra.Id}", new { codigo = otra.Codigo, nombre = otra.Nombre, categoriaPadreId = padre.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errores = (await response.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("errors");
        Assert.True(errores.TryGetProperty("CategoriaPadreId", out _));
    }

    [Fact(Timeout = 30_000)]
    public async Task Update_HaciaUnCicloPreexistenteEnLaBase_TerminaConRespuestaControlada_SinColgarse()
    {
        // Ciclo X↔Y metido saltándose el handler (dato corrupto). Recorrer la cadena de padres sin
        // conjunto de visitados giraría para siempre; el Timeout hace que ese bucle falle en vez de colgar la suite.
        Guid xId;
        await using (var contexto = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_fixture.AppConnectionString).Options))
        {
            var x = new CategoriaProducto { Codigo = CodigoUnico("CX"), Nombre = "X" };
            contexto.CategoriasProducto.Add(x);
            await contexto.SaveChangesAsync();
            var y = new CategoriaProducto { Codigo = CodigoUnico("CY"), Nombre = "Y", CategoriaPadreId = x.Id };
            contexto.CategoriasProducto.Add(y);
            await contexto.SaveChangesAsync();
            x.CategoriaPadreId = y.Id;
            await contexto.SaveChangesAsync();
            xId = x.Id;
        }

        var client = CreateClient("Administrador");
        var z = await Crear(client, CodigoUnico("CZ"));

        // Z no pertenece al ciclo: colgarla de X es válido y debe responder, no colgarse.
        var hacia = await client.PutAsJsonAsync($"{Ruta}/{z.Id}", new { codigo = z.Codigo, nombre = z.Nombre, categoriaPadreId = xId });
        Assert.Equal(HttpStatusCode.OK, hacia.StatusCode);

        // Cerrar un ciclo nuevo (X con padre Z, que ya cuelga de X) sigue detectándose.
        var x2 = await client.GetFromJsonAsync<CategoriaProductoResponse>($"{Ruta}/{xId}");
        var cierra = await client.PutAsJsonAsync($"{Ruta}/{xId}", new { codigo = x2!.Codigo, nombre = x2.Nombre, categoriaPadreId = z.Id });
        Assert.Equal(HttpStatusCode.BadRequest, cierra.StatusCode);
    }

    [Fact]
    public async Task Update_ComoPropioPadre_Devuelve400()
    {
        var client = CreateClient("Administrador");
        var a = await Crear(client, CodigoUnico("S"));

        var response = await client.PutAsJsonAsync($"{Ruta}/{a.Id}", new { codigo = a.Codigo, nombre = a.Nombre, categoriaPadreId = a.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_QueCierraUnCicloDeTresNiveles_Devuelve400_SinCorromperLaJerarquia()
    {
        var client = CreateClient("Administrador");
        var a = await Crear(client, CodigoUnico("CA"));
        var b = await Crear(client, CodigoUnico("CB"), padreId: a.Id);
        var c = await Crear(client, CodigoUnico("CC"), padreId: b.Id);

        var response = await client.PutAsJsonAsync($"{Ruta}/{a.Id}", new { codigo = a.Codigo, nombre = a.Nombre, categoriaPadreId = c.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errores = (await response.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("errors");
        Assert.True(errores.TryGetProperty("CategoriaPadreId", out _));
        Assert.Null((await client.GetFromJsonAsync<CategoriaProductoResponse>($"{Ruta}/{a.Id}"))!.CategoriaPadreId);
    }

    [Fact]
    public async Task Delete_ConHijos_Devuelve409_YTrasBorrarLosHijosYaSePuede()
    {
        var client = CreateClient("Administrador");
        var padre = await Crear(client, CodigoUnico("DP"));
        var hija = await Crear(client, CodigoUnico("DH"), padreId: padre.Id);

        var rechazado = await client.DeleteAsync($"{Ruta}/{padre.Id}");

        Assert.Equal(HttpStatusCode.Conflict, rechazado.StatusCode);
        var errores = (await rechazado.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("errors");
        Assert.True(errores.TryGetProperty("Id", out _));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Ruta}/{padre.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Ruta}/{hija.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Ruta}/{padre.Id}")).StatusCode);
    }

    [Fact]
    public async Task Create_NormalizaElCodigoAMayusculas()
    {
        var codigo = $"q{Guid.NewGuid():N}"[..8];

        var creada = await Crear(CreateClient("Administrador"), codigo);

        Assert.Equal(codigo.ToUpperInvariant(), creada.Codigo);
    }

    [Fact]
    public async Task List_IncluyeLaCategoriaGeneralSembradaPorLaMigracion()
    {
        var response = await CreateClient("Administrador").GetAsync($"{Ruta}?tamanoPagina=200&codigo=GENERAL");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var paged = await response.Content.ReadFromJsonAsync<PagedResult<CategoriaProductoResponse>>();
        var general = Assert.Single(paged!.Items, x => x.Codigo == "GENERAL");
        Assert.Equal(Guid.Parse("c1000000-0000-0000-0000-000000000001"), general.Id);
        Assert.Equal("General", general.Nombre);
        Assert.Null(general.CategoriaPadreId);
    }

    [Fact]
    public async Task List_RespetaElTamanoDePaginaYDevuelveElTotal()
    {
        var client = CreateClient("Administrador");
        for (var i = 0; i < 3; i++)
        {
            await Crear(client, CodigoUnico("P"), $"Pag{i}");
        }

        var paged = await client.GetFromJsonAsync<PagedResult<CategoriaProductoResponse>>($"{Ruta}?tamanoPagina=2&pagina=1");

        Assert.Equal(2, paged!.Items.Count);
        Assert.True(paged.Total >= 3);
        Assert.True(paged.TotalPaginas >= 2);
    }

    [Fact]
    public async Task List_ColumnaDeOrdenNoPermitida_CaeAlOrdenPorDefectoSinRomper()
    {
        var response = await CreateClient("Administrador").GetAsync(
            $"{Ruta}?ordenarPor=" + Uri.EscapeDataString("\"; DROP TABLE \"CategoriasProducto") + "&tamanoPagina=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string CodigoUnico(string prefijo) => $"{prefijo}{Guid.NewGuid():N}"[..12];

    private static async Task<CategoriaProductoResponse> Crear(
        HttpClient client, string codigo, string nombre = "Categoría", Guid? padreId = null)
    {
        var response = await client.PostAsJsonAsync(Ruta, new { codigo, nombre, categoriaPadreId = padreId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CategoriaProductoResponse>())!;
    }

    [Fact]
    public async Task Listado_Y_GetPorId_NoDevuelvenUnaCategoriaBorradaLogicamente()
    {
        // Protege el predicado "IsDeleted" = false de DapperCategoriaProductoReadRepository (lista y GET por Id).
        var client = CreateClient("Administrador");
        var marca = Guid.NewGuid().ToString("N")[..8];

        var conservada = await Crear(client, $"L{marca}A");
        var borrada = await Crear(client, $"L{marca}B");
        Assert.Equal(2, (await client.GetFromJsonAsync<PagedResult<CategoriaProductoResponse>>($"{Ruta}?codigo={marca}"))!.Total);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Ruta}/{borrada.Id}")).StatusCode);

        var paged = (await client.GetFromJsonAsync<PagedResult<CategoriaProductoResponse>>($"{Ruta}?codigo={marca}"))!;
        Assert.Equal(1, paged.Total);
        Assert.Equal(conservada.Id, Assert.Single(paged.Items).Id);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Ruta}/{conservada.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Ruta}/{borrada.Id}")).StatusCode);
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
