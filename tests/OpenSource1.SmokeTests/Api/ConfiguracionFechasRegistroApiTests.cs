using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenSource1.Application.Features.FechasRegistro.Dtos;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

/// <summary>
/// <c>api/configuracion/fechas-registro</c> (Task 8.5) contra Postgres real: solo el Administrador consulta y configura el rango
/// general y las excepciones por usuario (Supervisor y Ejecutor 403); validaciones (400/404/409); y de punta a punta, que un
/// posteo por la API usa la excepción del usuario autenticado (claim NameIdentifier) o, si no tiene, el rango general.
/// REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ConfiguracionFechasRegistroApiTests : IClassFixture<PostgresTestFixture>
{
    private const string Ruta = "/api/configuracion/fechas-registro";
    private static readonly DateOnly D1 = new(2026, 9, 1);
    private static readonly DateOnly D5 = new(2026, 9, 5);
    private static readonly DateOnly D10 = new(2026, 9, 10);

    private readonly HttpClient _client;

    public ConfiguracionFechasRegistroApiTests(PostgresTestFixture fixture)
    {
        _client = fixture.CreateFactory().CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Theory]
    [InlineData("Supervisor")]
    [InlineData("Ejecutor")]
    public async Task SupervisorYEjecutor_NoConsultanNiConfiguran_403(string rol)
    {
        var client = Rol(rol);
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Ruta)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(Ruta, new { permitirRegistroDesde = D1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Ruta}/usuarios")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Ruta}/usuarios/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"{Ruta}/usuarios", new { usuarioId = id })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"{Ruta}/usuarios/{id}", new { permitirRegistroDesde = D1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"{Ruta}/usuarios/{id}")).StatusCode);

        var anon = new HttpRequestMessage(HttpMethod.Get, Ruta);
        anon.Headers.Add("X-Test-Anonymous", "true");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(anon)).StatusCode);
    }

    [Fact]
    public async Task Administrador_General_YUsuarios_Crud_ConValidaciones()
    {
        var admin = Rol("Administrador");
        try
        {
            // General: la semilla no tiene límites.
            var general = (await admin.GetFromJsonAsync<FechasRegistroGeneralResponse>(Ruta))!;
            Assert.Equal((null, null), (general.PermitirRegistroDesde, general.PermitirRegistroHasta));

            await AssertErrorAsync(
                await admin.PutAsJsonAsync(Ruta, new { permitirRegistroDesde = D10, permitirRegistroHasta = D1 }),
                HttpStatusCode.BadRequest, "PermitirRegistroHasta", "posterior");
            var modificado = await admin.PutAsJsonAsync(Ruta, new { permitirRegistroDesde = D1, permitirRegistroHasta = (DateOnly?)null });
            Assert.True(modificado.StatusCode == HttpStatusCode.OK, await modificado.Content.ReadAsStringAsync());
            general = (await admin.GetFromJsonAsync<FechasRegistroGeneralResponse>(Ruta))!;
            Assert.Equal((D1, (DateOnly?)null), (general.PermitirRegistroDesde, general.PermitirRegistroHasta));
            Assert.Equal("administrador", general.UpdatedBy);

            // Usuarios: alta de un usuario real de Identity con su correo desnormalizado.
            var (usuarioId, email) = await CrearUsuarioIdentityAsync(admin);
            await AssertErrorAsync(
                await admin.PostAsJsonAsync($"{Ruta}/usuarios", new { usuarioId = Guid.NewGuid(), permitirRegistroDesde = D1 }),
                HttpStatusCode.BadRequest, "UsuarioId", "no existe");
            await AssertErrorAsync(
                await admin.PostAsJsonAsync($"{Ruta}/usuarios", new { usuarioId, permitirRegistroDesde = D10, permitirRegistroHasta = D5 }),
                HttpStatusCode.BadRequest, "PermitirRegistroHasta");

            var creado = await admin.PostAsJsonAsync($"{Ruta}/usuarios", new { usuarioId, permitirRegistroDesde = D1, permitirRegistroHasta = D5 });
            Assert.True(creado.StatusCode == HttpStatusCode.Created, await creado.Content.ReadAsStringAsync());
            var fila = (await creado.Content.ReadFromJsonAsync<FechasRegistroUsuarioResponse>())!;
            Assert.Equal((usuarioId, email, D1, D5), (fila.UsuarioId, fila.NombreUsuario, fila.PermitirRegistroDesde, fila.PermitirRegistroHasta));

            await AssertErrorAsync(
                await admin.PostAsJsonAsync($"{Ruta}/usuarios", new { usuarioId, permitirRegistroDesde = D1 }),
                HttpStatusCode.Conflict, "UsuarioId");

            var lista = (await admin.GetFromJsonAsync<List<FechasRegistroUsuarioResponse>>($"{Ruta}/usuarios"))!;
            Assert.Contains(lista, x => x.Id == fila.Id && x.UsuarioId == usuarioId);

            // Modificación (solo el rango) y lectura.
            await AssertErrorAsync(
                await admin.PutAsJsonAsync($"{Ruta}/usuarios/{fila.Id}", new { permitirRegistroDesde = D10, permitirRegistroHasta = D5 }),
                HttpStatusCode.BadRequest, "PermitirRegistroHasta");
            var editado = await admin.PutAsJsonAsync($"{Ruta}/usuarios/{fila.Id}", new { permitirRegistroDesde = (DateOnly?)null, permitirRegistroHasta = D10 });
            Assert.True(editado.StatusCode == HttpStatusCode.OK, await editado.Content.ReadAsStringAsync());
            var leido = (await admin.GetFromJsonAsync<FechasRegistroUsuarioResponse>($"{Ruta}/usuarios/{fila.Id}"))!;
            Assert.Equal(((DateOnly?)null, (DateOnly?)D10), (leido.PermitirRegistroDesde, leido.PermitirRegistroHasta));
            Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"{Ruta}/usuarios/{Guid.NewGuid()}", new { permitirRegistroDesde = D1 })).StatusCode);

            // Borrado: 204, luego 404; y se puede volver a crear para el mismo usuario.
            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Ruta}/usuarios/{fila.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Ruta}/usuarios/{fila.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"{Ruta}/usuarios/{fila.Id}")).StatusCode);
            var recreado = await admin.PostAsJsonAsync($"{Ruta}/usuarios", new { usuarioId });
            Assert.Equal(HttpStatusCode.Created, recreado.StatusCode);
            var idRecreado = (await recreado.Content.ReadFromJsonAsync<FechasRegistroUsuarioResponse>())!.Id;
            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Ruta}/usuarios/{idRecreado}")).StatusCode);
        }
        finally
        {
            await admin.PutAsJsonAsync(Ruta, new { permitirRegistroDesde = (DateOnly?)null, permitirRegistroHasta = (DateOnly?)null });
        }
    }

    [Fact]
    public async Task PosteoPorLaApi_UsaLaExcepcionDelUsuarioAutenticado_OElRangoGeneral()
    {
        var admin = Rol("Administrador");
        var (usuarioId, _) = await CrearUsuarioIdentityAsync(admin);
        var socio = await CrearSocioAsync(admin);
        var pago = new { socioNegocioId = socio, importe = 10m, fechaRegistro = D10 };
        Guid? excepcion = null;
        try
        {
            // General hasta el 05/09: el pago del 10/09 de un usuario sin excepción -> 400 en FechaRegistro, con el rango general.
            Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(Ruta, new { permitirRegistroDesde = D1, permitirRegistroHasta = D5 })).StatusCode);
            await AssertErrorAsync(await Como("Supervisor", usuarioId).PostAsJsonAsync("/api/cobros", pago),
                HttpStatusCode.BadRequest, "FechaRegistro", "del 01/09/2026 al 05/09/2026");

            // Con su excepción (más amplia) el mismo usuario sí registra; otro usuario sigue con el general.
            var creado = await Rol("Administrador").PostAsJsonAsync($"{Ruta}/usuarios", new { usuarioId, permitirRegistroDesde = D1, permitirRegistroHasta = D10 });
            Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
            excepcion = (await creado.Content.ReadFromJsonAsync<FechasRegistroUsuarioResponse>())!.Id;
            var ok = await Como("Supervisor", usuarioId).PostAsJsonAsync("/api/cobros", pago);
            Assert.True(ok.StatusCode == HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
            await AssertErrorAsync(await Como("Supervisor", Guid.NewGuid()).PostAsJsonAsync("/api/cobros", pago),
                HttpStatusCode.BadRequest, "FechaRegistro", "general");
        }
        finally
        {
            var limpio = Rol("Administrador");
            if (excepcion is { } id)
            {
                await limpio.DeleteAsync($"{Ruta}/usuarios/{id}");
            }

            await limpio.PutAsJsonAsync(Ruta, new { permitirRegistroDesde = (DateOnly?)null, permitirRegistroHasta = (DateOnly?)null });
        }
    }

    // ----- Helpers -----

    private static async Task<(Guid Id, string Email)> CrearUsuarioIdentityAsync(HttpClient admin)
    {
        var email = $"fechas-{Guid.NewGuid():N}@test.local";
        var creado = await admin.PostAsJsonAsync("/api/users", new { email, fullName = "Usuario fechas", password = "Password123" });
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
        var usuarios = JsonDocument.Parse(await admin.GetStringAsync($"/api/users?search={Uri.EscapeDataString(email)}")).RootElement;
        var id = usuarios.EnumerateArray().Single(x => x.GetProperty("email").GetString() == email).GetProperty("id").GetString();
        return (Guid.Parse(id!), email);
    }

    private static async Task<Guid> CrearSocioAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = "Cliente fechas de registro" });
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string campo, string? fragmento = null)
    {
        var cuerpo = await response.Content.ReadAsStringAsync();
        Assert.True(status == response.StatusCode, $"Se esperaba {status} y llegó {response.StatusCode}: {cuerpo}");
        Assert.True(
            JsonDocument.Parse(cuerpo).RootElement.GetProperty("errors").TryGetProperty(campo, out _),
            $"Se esperaba el campo '{campo}' en los errores: {cuerpo}");
        if (fragmento is not null)
        {
            Assert.Contains(fragmento, cuerpo);
        }
    }

    private HttpClient Rol(string role) => Como(role, null);

    private HttpClient Como(string role, Guid? usuarioId)
    {
        var client = _client;
        client.DefaultRequestHeaders.Remove("X-Test-Anonymous");
        client.DefaultRequestHeaders.Remove("X-Test-User");
        client.DefaultRequestHeaders.Remove("X-Test-Roles");
        client.DefaultRequestHeaders.Remove("X-Test-UserId");
        client.DefaultRequestHeaders.Add("X-Test-User", role.ToLowerInvariant());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        if (usuarioId is { } id)
        {
            client.DefaultRequestHeaders.Add("X-Test-UserId", id.ToString());
        }

        return client;
    }
}
