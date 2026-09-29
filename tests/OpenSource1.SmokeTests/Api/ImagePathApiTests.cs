using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Application.Services.Auth.Dtos;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

/// <summary>
/// El <c>imagePath</c> que acepta la API acaba en un borrado de ficheros al sustituir la imagen (Blazor). Cualquier usuario con
/// permiso de alta/modificación (o cualquier autenticado, en el perfil) podía guardar una ruta hostil: la API debe rechazarla con
/// 400 y el campo <c>ImagePath</c>, y aceptar la ruta legítima que genera la UI.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ImagePathApiTests : IClassFixture<PostgresTestFixture>
{
    private readonly HttpClient _client;

    public ImagePathApiTests(PostgresTestFixture fixture)
    {
        _client = fixture.CreateFactory().CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    public static TheoryData<string> RutasHostiles => new()
    {
        "../../../../tmp/senuelo.txt",
        "..\\..\\senuelo.txt",
        "/etc/passwd",
        "/uploads/clientes/../../../senuelo.txt",
        "/uploads/clientes/%2e%2e/senuelo.png",
        "/uploads/clientes/a/b.png",
        "appsettings.json",
        "   ",
    };

    // Válida para OTRA entidad, no para la que se prueba: un cliente no puede apuntar a la imagen de un producto o de un usuario.
    [Theory]
    [InlineData("/uploads/users/perfil-1.png")]
    [InlineData("/uploads/productos/producto-1.png")]
    public async Task Socios_ImagePathDeOtraEntidad_Devuelve400(string ruta) =>
        await AssertImagePath400Async(await Cliente("Administrador").PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = "x", imagePath = ruta }));

    [Theory]
    [InlineData("/uploads/users/perfil-1.png")]
    [InlineData("/uploads/clientes/cliente-1.png")]
    public async Task Productos_ImagePathDeOtraEntidad_Devuelve400(string ruta) =>
        await AssertImagePath400Async(await Cliente("Administrador").PostAsJsonAsync("/api/productos", CuerpoProducto(ruta)));

    [Theory]
    [InlineData("/uploads/clientes/cliente-1.png")]
    [InlineData("/uploads/productos/producto-1.png")]
    public async Task Perfil_ImagePathDeOtraEntidad_Devuelve400(string ruta) =>
        await AssertImagePath400Async(await Cliente("Ejecutor").PostAsJsonAsync("/api/auth/profile-image", new UpdateProfileImageRequest(ruta)));

    // Una imagen solo puede tener un dueño: cada caso usa un nombre propio, como haría la UI (GUID).
    private static string RutaCliente() => $"/uploads/clientes/cliente-{Guid.NewGuid():N}.png";
    private static string RutaProducto() => $"/uploads/productos/producto-{Guid.NewGuid():N}.png";
    private static string RutaPerfil() => $"/uploads/users/perfil-{Guid.NewGuid():N}.png";

    // ---------- Socios de negocio ----------

    [Theory]
    [MemberData(nameof(RutasHostiles))]
    public async Task Socios_Post_ConImagePathHostil_Devuelve400ConElCampo(string ruta)
    {
        var client = Cliente("Administrador");

        var respuesta = await client.PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = "Img hostil", imagePath = ruta });

        await AssertImagePath400Async(respuesta);
    }

    [Theory]
    [MemberData(nameof(RutasHostiles))]
    public async Task Socios_Put_ConImagePathHostil_Devuelve400ConElCampo_YNoCambiaElRegistro(string ruta)
    {
        var client = Cliente("Administrador");
        var propia = RutaCliente();
        var creado = await CrearSocioAsync(client, propia);

        var respuesta = await client.PutAsJsonAsync($"/api/socios-negocio/{creado.Id}", new { nombreComercial = "Img hostil", imagePath = ruta });

        await AssertImagePath400Async(respuesta);
        var leido = await (await client.GetAsync($"/api/socios-negocio/{creado.Id}")).Content.ReadFromJsonAsync<SocioNegocioResponse>();
        Assert.Equal(propia, leido!.ImagePath);
    }

    [Fact]
    public async Task Socios_ConImagePathLegitimoOVacio_Funciona()
    {
        var client = Cliente("Administrador");

        var legitima = RutaCliente();
        var creado = await CrearSocioAsync(client, legitima);
        Assert.Equal(legitima, creado.ImagePath);

        var nueva = RutaCliente();
        var put = await client.PutAsJsonAsync($"/api/socios-negocio/{creado.Id}", new { nombreComercial = "Img legítima", imagePath = nueva });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(nueva, (await put.Content.ReadFromJsonAsync<SocioNegocioResponse>())!.ImagePath);

        var sinImagen = await client.PutAsJsonAsync($"/api/socios-negocio/{creado.Id}", new { nombreComercial = "Sin imagen" });
        Assert.Equal(HttpStatusCode.OK, sinImagen.StatusCode);
        Assert.Null((await sinImagen.Content.ReadFromJsonAsync<SocioNegocioResponse>())!.ImagePath);
    }

    [Fact]
    public async Task Socios_ImagePathHostil_LoRechazaTambienUnSupervisorYUnEjecutor()
    {
        var creado = await CrearSocioAsync(Cliente("Administrador"), RutaCliente());
        // Supervisor: solo CanModify/CanConsult, es quien explotaba la edición.
        var supervisor = await Cliente("Supervisor").PutAsJsonAsync($"/api/socios-negocio/{creado.Id}", new { nombreComercial = "x", imagePath = "../../x" });
        await AssertImagePath400Async(supervisor);

        var ejecutor = await Cliente("Ejecutor").PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = "x", imagePath = "../../x" });
        await AssertImagePath400Async(ejecutor);
    }

    // ---------- Productos ----------

    [Theory]
    [MemberData(nameof(RutasHostiles))]
    public async Task Productos_Post_ConImagePathHostil_Devuelve400ConElCampo(string ruta)
    {
        var client = Cliente("Administrador");

        var respuesta = await client.PostAsJsonAsync("/api/productos", CuerpoProducto(ruta));

        await AssertImagePath400Async(respuesta);
    }

    [Theory]
    [MemberData(nameof(RutasHostiles))]
    public async Task Productos_Put_ConImagePathHostil_Devuelve400ConElCampo_YNoCambiaElRegistro(string ruta)
    {
        var client = Cliente("Administrador");
        var propia = RutaProducto();
        var creado = await CrearProductoAsync(client, propia);

        var respuesta = await client.PutAsJsonAsync($"/api/productos/{creado.Id}", CuerpoProducto(ruta, creado.Codigo));

        await AssertImagePath400Async(respuesta);
        var leido = await (await client.GetAsync($"/api/productos/{creado.Id}")).Content.ReadFromJsonAsync<ProductoResponse>();
        Assert.Equal(propia, leido!.ImagePath);
    }

    [Fact]
    public async Task Productos_ConImagePathLegitimoOVacio_Funciona()
    {
        var client = Cliente("Administrador");

        var legitima = RutaProducto();
        var creado = await CrearProductoAsync(client, legitima);
        Assert.Equal(legitima, creado.ImagePath);

        var put = await client.PutAsJsonAsync($"/api/productos/{creado.Id}", CuerpoProducto(null, creado.Codigo));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Null((await put.Content.ReadFromJsonAsync<ProductoResponse>())!.ImagePath);
    }

    [Fact]
    public async Task Productos_ImagePathHostil_LoRechazaUnEjecutorEnElAlta()
    {
        var respuesta = await Cliente("Ejecutor").PostAsJsonAsync("/api/productos", CuerpoProducto("../../x"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    // ---------- Perfil de usuario ----------

    [Theory]
    [MemberData(nameof(RutasHostiles))]
    public async Task Perfil_ImagePathHostil_Devuelve400ConElCampo_ParaCualquierUsuarioAutenticado(string ruta)
    {
        // Un Ejecutor (solo CanConsult y CanAdd) no debe poder guardar una ruta hostil en su perfil.
        var respuesta = await Cliente("Ejecutor").PostAsJsonAsync("/api/auth/profile-image", new UpdateProfileImageRequest(ruta));

        await AssertImagePath400Async(respuesta);
    }

    [Fact]
    public async Task Perfil_ImagePathLegitimoOVacio_Devuelve204()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { UserNameOrEmail = "ejecutor", Password = "Password123" });
        login.EnsureSuccessStatusCode();
        var userId = (await login.Content.ReadFromJsonAsync<AuthResponse>())!.UserId;
        var client = Cliente("Ejecutor", userId);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/profile-image", new UpdateProfileImageRequest(RutaPerfil()))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/profile-image", new UpdateProfileImageRequest(null))).StatusCode);
    }

    // ---------- Una imagen, un dueño (evita adoptar el fichero de otro registro y borrarlo al sustituir la imagen) ----------

    [Fact]
    public async Task Socios_ImagePathYaAsignadoAOtroSocio_Devuelve400_PeroElPropioSePermite()
    {
        var client = Cliente("Administrador");
        var ruta = RutaCliente();
        var dueno = await CrearSocioAsync(client, ruta);

        await AssertImagePath400Async(await client.PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = "Adopta", imagePath = ruta }));

        var otro = await CrearSocioAsync(client, RutaCliente());
        await AssertImagePath400Async(await client.PutAsJsonAsync($"/api/socios-negocio/{otro.Id}", new { nombreComercial = "Adopta", imagePath = ruta }));

        // El dueño puede reenviar su propia imagen (es lo que hace la UI al editar sin subir otra).
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/socios-negocio/{dueno.Id}", new { nombreComercial = "Dueño", imagePath = ruta })).StatusCode);
    }

    [Fact]
    public async Task Productos_ImagePathYaAsignadoAOtroProducto_Devuelve400_PeroElPropioSePermite()
    {
        var client = Cliente("Administrador");
        var ruta = RutaProducto();
        var dueno = await CrearProductoAsync(client, ruta);

        await AssertImagePath400Async(await client.PostAsJsonAsync("/api/productos", CuerpoProducto(ruta)));

        var otro = await CrearProductoAsync(client, RutaProducto());
        await AssertImagePath400Async(await client.PutAsJsonAsync($"/api/productos/{otro.Id}", CuerpoProducto(ruta, otro.Codigo)));

        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/productos/{dueno.Id}", CuerpoProducto(ruta, dueno.Codigo))).StatusCode);
    }

    [Fact]
    public async Task Perfil_ImagePathYaAsignadoAOtroUsuario_Devuelve400_PeroElPropioSePermite()
    {
        var ruta = $"/uploads/users/perfil-{Guid.NewGuid():N}.png";
        var supervisor = Cliente("Supervisor", await UserIdAsync("supervisor"));
        Assert.Equal(HttpStatusCode.NoContent, (await supervisor.PostAsJsonAsync("/api/auth/profile-image", new UpdateProfileImageRequest(ruta))).StatusCode);
        // Vuelve a enviar la suya: 204.
        Assert.Equal(HttpStatusCode.NoContent, (await supervisor.PostAsJsonAsync("/api/auth/profile-image", new UpdateProfileImageRequest(ruta))).StatusCode);

        var ejecutor = Cliente("Ejecutor", await UserIdAsync("ejecutor"));
        await AssertImagePath400Async(await ejecutor.PostAsJsonAsync("/api/auth/profile-image", new UpdateProfileImageRequest(ruta)));

        // Limpieza: deja libre la ruta.
        Assert.Equal(HttpStatusCode.NoContent, (await Cliente("Supervisor", await UserIdAsync("supervisor")).PostAsJsonAsync("/api/auth/profile-image", new UpdateProfileImageRequest(null))).StatusCode);
    }

    private async Task<string> UserIdAsync(string userName)
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { UserNameOrEmail = userName, Password = "Password123" });
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<AuthResponse>())!.UserId;
    }

    // ---------- Helpers ----------

    private static async Task AssertImagePath400Async(HttpResponseMessage respuesta)
    {
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.StatusCode == HttpStatusCode.BadRequest, $"Se esperaba 400 y fue {(int)respuesta.StatusCode}: {cuerpo}");
        var errores = JsonDocument.Parse(cuerpo).RootElement.GetProperty("errors");
        Assert.True(errores.ValueKind == JsonValueKind.Object && errores.TryGetProperty("ImagePath", out _), $"El 400 debe señalar el campo ImagePath: {cuerpo}");
    }

    private static async Task<SocioNegocioResponse> CrearSocioAsync(HttpClient client, string imagePath)
    {
        var respuesta = await client.PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = "Con imagen", imagePath });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<SocioNegocioResponse>())!;
    }

    private static object CuerpoProducto(string? imagePath, string? codigo = null) => new
    {
        codigo = codigo ?? $"IMG-{Guid.NewGuid():N}"[..20],
        nombre = "Prod imagen",
        precioVenta = 1m,
        stock = 1,
        imagePath,
    };

    private static async Task<ProductoResponse> CrearProductoAsync(HttpClient client, string imagePath)
    {
        var respuesta = await client.PostAsJsonAsync("/api/productos", CuerpoProducto(imagePath));
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<ProductoResponse>())!;
    }

    private HttpClient Cliente(string rol, string? usuario = null)
    {
        var client = _client;
        client.DefaultRequestHeaders.Remove("X-Test-Anonymous");
        client.DefaultRequestHeaders.Remove("X-Test-User");
        client.DefaultRequestHeaders.Remove("X-Test-Roles");
        client.DefaultRequestHeaders.Add("X-Test-User", usuario ?? rol.ToLowerInvariant());
        client.DefaultRequestHeaders.Add("X-Test-Roles", rol);
        return client;
    }
}
