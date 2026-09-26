using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenSource1.Application.Features.CuentasContables.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

[Collection(PostgresCollection.Name)]
public sealed class CuentasContablesApiTests : IClassFixture<PostgresTestFixture>
{
    private readonly HttpClient _client;

    public CuentasContablesApiTests(PostgresTestFixture fixture)
    {
        var factory = fixture.CreateFactory();
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Crud_And_Authorization_Work()
    {
        var anon = new HttpRequestMessage(HttpMethod.Get, "/api/cuentas-contables");
        anon.Headers.Add("X-Test-Anonymous", "true");
        var anonymousResponse = await _client.SendAsync(anon);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        var supervisor = CreateClient("Supervisor");
        var numeroForbidden = NuevoNumero();
        var forbidden = await supervisor.PostAsJsonAsync(
            "/api/cuentas-contables",
            new { numero = numeroForbidden, nombre = "No debería crearse", tipoCuenta = TipoCuentaContable.Posteo, tipoResultado = TipoResultadoCuenta.Balance, posteoDirecto = false, bloqueada = false, sangria = 0 });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // Supervisor SÍ puede consultar.
        Assert.Equal(HttpStatusCode.OK, (await supervisor.GetAsync("/api/cuentas-contables")).StatusCode);

        var client = CreateClient("Administrador");
        var numero = NuevoNumero();
        var create = await client.PostAsJsonAsync(
            "/api/cuentas-contables",
            new { numero, nombre = "Cuenta de prueba", tipoCuenta = TipoCuentaContable.Posteo, tipoResultado = TipoResultadoCuenta.Balance, posteoDirecto = true, bloqueada = false, sangria = 1 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var creada = await create.Content.ReadFromJsonAsync<CuentaContableResponse>();
        Assert.Equal(numero, creada!.Numero);
        Assert.Equal(TipoCuentaContable.Posteo, creada.TipoCuenta);
        Assert.Equal(TipoResultadoCuenta.Balance, creada.TipoResultado);
        Assert.True(creada.PosteoDirecto);
        Assert.True(creada.Xmin > 0);

        var list = await client.GetAsync("/api/cuentas-contables?tamanoPagina=200");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var paged = await list.Content.ReadFromJsonAsync<PagedResult<CuentaContableResponse>>();
        Assert.NotNull(paged);
        Assert.Contains(paged!.Items, x => x.Id == creada.Id);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/cuentas-contables/{creada.Id}")).StatusCode);

        // Ejecutor no tiene CanModify.
        var putForbidden = await CreateClient("Ejecutor").PutAsJsonAsync(
            $"/api/cuentas-contables/{creada.Id}",
            new { numero, nombre = "Renombrada", tipoCuenta = TipoCuentaContable.Posteo, tipoResultado = TipoResultadoCuenta.Balance, posteoDirecto = (bool?)null, bloqueada = (bool?)null, sangria = (int?)null, xmin = creada.Xmin });
        Assert.Equal(HttpStatusCode.Forbidden, putForbidden.StatusCode);

        // Supervisor SÍ tiene CanModify.
        var update = await CreateClient("Supervisor").PutAsJsonAsync(
            $"/api/cuentas-contables/{creada.Id}",
            new { numero, nombre = "Cuenta renombrada", tipoCuenta = TipoCuentaContable.Posteo, tipoResultado = TipoResultadoCuenta.Balance, posteoDirecto = (bool?)null, bloqueada = (bool?)null, sangria = (int?)null, xmin = creada.Xmin });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var actualizada = await update.Content.ReadFromJsonAsync<CuentaContableResponse>();
        Assert.Equal("Cuenta renombrada", actualizada!.Nombre);
        // Campos no informados (null) se conservan.
        Assert.True(actualizada.PosteoDirecto);
        Assert.Equal(1, actualizada.Sangria);
        Assert.True(actualizada.Xmin != creada.Xmin);

        // "client" es la misma instancia compartida que CreateClient() reconfigura en cada llamada (ver
        // CreateClient): se vuelve a fijar el rol Administrador explícitamente en vez de asumir que "client"
        // todavía lo tiene tras los CreateClient("Ejecutor"/"Supervisor") de arriba.
        var administrador = CreateClient("Administrador");
        Assert.Equal(HttpStatusCode.NotFound, (await administrador.GetAsync($"/api/cuentas-contables/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await administrador.DeleteAsync($"/api/cuentas-contables/{Guid.NewGuid()}")).StatusCode);

        // Solo Administrador puede borrar.
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Supervisor").DeleteAsync($"/api/cuentas-contables/{creada.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Ejecutor").DeleteAsync($"/api/cuentas-contables/{creada.Id}")).StatusCode);

        // Sin uso (cuenta recién creada: ningún grupo de cliente ni setup la referencia): el borrado se permite -> 204.
        administrador = CreateClient("Administrador");
        Assert.Equal(HttpStatusCode.NoContent, (await administrador.DeleteAsync($"/api/cuentas-contables/{creada.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await administrador.GetAsync($"/api/cuentas-contables/{creada.Id}")).StatusCode);

        var listaTrasBorrar = await ListarPorNumeroAsync(administrador, numero);
        Assert.Equal(0, listaTrasBorrar.Total);
    }

    [Fact]
    public async Task Update_ConXminDesactualizado_Devuelve409()
    {
        var client = CreateClient("Administrador");
        var numero = NuevoNumero();

        var create = await client.PostAsJsonAsync(
            "/api/cuentas-contables",
            new { numero, nombre = "Original", tipoCuenta = TipoCuentaContable.Posteo, tipoResultado = TipoResultadoCuenta.Balance, posteoDirecto = false, bloqueada = false, sangria = 0 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var creada = await create.Content.ReadFromJsonAsync<CuentaContableResponse>();

        var primeraModificacion = await client.PutAsJsonAsync(
            $"/api/cuentas-contables/{creada!.Id}",
            new { numero, nombre = "Modificada", tipoCuenta = TipoCuentaContable.Posteo, tipoResultado = TipoResultadoCuenta.Balance, posteoDirecto = (bool?)null, bloqueada = (bool?)null, sangria = (int?)null, xmin = creada.Xmin });
        Assert.Equal(HttpStatusCode.OK, primeraModificacion.StatusCode);

        // PUT con el Xmin viejo (desactualizado) -> 409.
        var conflicto = await client.PutAsJsonAsync(
            $"/api/cuentas-contables/{creada.Id}",
            new { numero, nombre = "Otro nombre", tipoCuenta = TipoCuentaContable.Posteo, tipoResultado = TipoResultadoCuenta.Balance, posteoDirecto = (bool?)null, bloqueada = (bool?)null, sangria = (int?)null, xmin = creada.Xmin });
        Assert.Equal(HttpStatusCode.Conflict, conflicto.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("AB01")]
    [InlineData("123456789012345678901")]
    public async Task Create_ConNumeroInvalido_Devuelve400ConCampoNumero(string numero)
    {
        var client = CreateClient("Administrador");

        var response = await client.PostAsJsonAsync(
            "/api/cuentas-contables",
            new { numero, nombre = "Nombre", tipoCuenta = TipoCuentaContable.Posteo, tipoResultado = TipoResultadoCuenta.Balance, posteoDirecto = false, bloqueada = false, sangria = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problema = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.True(problema!.RootElement.GetProperty("errors").TryGetProperty("Numero", out _));
    }

    [Fact]
    public async Task Create_ConNumeroDuplicado_Devuelve409()
    {
        var client = CreateClient("Administrador");
        var numero = NuevoNumero();

        var primero = await client.PostAsJsonAsync(
            "/api/cuentas-contables",
            new { numero, nombre = "Original", tipoCuenta = TipoCuentaContable.Posteo, tipoResultado = TipoResultadoCuenta.Balance, posteoDirecto = false, bloqueada = false, sangria = 0 });
        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);

        var duplicado = await client.PostAsJsonAsync(
            "/api/cuentas-contables",
            new { numero, nombre = "Duplicado", tipoCuenta = TipoCuentaContable.Posteo, tipoResultado = TipoResultadoCuenta.Balance, posteoDirecto = false, bloqueada = false, sangria = 0 });
        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);
    }

    [Fact]
    public async Task Delete_SinUso_Devuelve204YLuegoGet404YElListadoNoLoDevuelve()
    {
        var client = CreateClient("Administrador");
        var numero = NuevoNumero();

        var create = await client.PostAsJsonAsync(
            "/api/cuentas-contables",
            new { numero, nombre = "Normal", tipoCuenta = TipoCuentaContable.Posteo, tipoResultado = TipoResultadoCuenta.Balance, posteoDirecto = false, bloqueada = false, sangria = 0 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<CuentaContableResponse>())!.Id;

        var delete = await client.DeleteAsync($"/api/cuentas-contables/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/cuentas-contables/{id}")).StatusCode);

        var lista = await ListarPorNumeroAsync(client, numero);
        Assert.Equal(0, lista.Total);
    }

    [Fact]
    public async Task Update_CambiaTipoCuentaDePosteoAOtroTipo_SinUso_SePermite()
    {
        var client = CreateClient("Administrador");
        var numero = NuevoNumero();

        var create = await client.PostAsJsonAsync(
            "/api/cuentas-contables",
            new { numero, nombre = "Provisional", tipoCuenta = TipoCuentaContable.Posteo, tipoResultado = TipoResultadoCuenta.Balance, posteoDirecto = false, bloqueada = false, sangria = 1 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var creada = await create.Content.ReadFromJsonAsync<CuentaContableResponse>();

        // Cuenta recién creada, sin uso (ningún grupo de cliente ni setup la referencia): el cambio de tipo se permite.
        var update = await client.PutAsJsonAsync(
            $"/api/cuentas-contables/{creada!.Id}",
            new { numero, nombre = "Provisional", tipoCuenta = TipoCuentaContable.Total, tipoResultado = TipoResultadoCuenta.Balance, posteoDirecto = (bool?)null, bloqueada = (bool?)null, sangria = (int?)null, xmin = creada.Xmin });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var actualizada = await update.Content.ReadFromJsonAsync<CuentaContableResponse>();
        Assert.Equal(TipoCuentaContable.Total, actualizada!.TipoCuenta);
    }

    [Fact]
    public async Task List_FiltraPorNumeroNombreTipoCuentaTipoResultadoYBloqueada()
    {
        var client = CreateClient("Administrador");
        var numero = NuevoNumero();
        var nombre = $"Filtrable {Guid.NewGuid():N}";

        var create = await client.PostAsJsonAsync(
            "/api/cuentas-contables",
            new { numero, nombre, tipoCuenta = TipoCuentaContable.Posteo, tipoResultado = TipoResultadoCuenta.Resultado, posteoDirecto = true, bloqueada = true, sangria = 2 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var creada = await create.Content.ReadFromJsonAsync<CuentaContableResponse>();

        var porNumero = await ListarAsync(client, $"numero={numero}");
        Assert.Single(porNumero.Items, x => x.Id == creada!.Id);

        var porNombre = await ListarAsync(client, $"nombre={Uri.EscapeDataString(nombre)}");
        Assert.Single(porNombre.Items, x => x.Id == creada!.Id);

        var porTipoCuenta = await ListarAsync(client, $"numero={numero}&tipoCuenta={(int)TipoCuentaContable.Posteo}");
        Assert.Contains(porTipoCuenta.Items, x => x.Id == creada!.Id);

        var porTipoCuentaEquivocado = await ListarAsync(client, $"numero={numero}&tipoCuenta={(int)TipoCuentaContable.Encabezado}");
        Assert.DoesNotContain(porTipoCuentaEquivocado.Items, x => x.Id == creada!.Id);

        var porTipoResultado = await ListarAsync(client, $"numero={numero}&tipoResultado={(int)TipoResultadoCuenta.Resultado}");
        Assert.Contains(porTipoResultado.Items, x => x.Id == creada!.Id);

        var porBloqueada = await ListarAsync(client, $"numero={numero}&bloqueada=true");
        Assert.Contains(porBloqueada.Items, x => x.Id == creada!.Id);

        var porNoBloqueada = await ListarAsync(client, $"numero={numero}&bloqueada=false");
        Assert.DoesNotContain(porNoBloqueada.Items, x => x.Id == creada!.Id);
    }

    // Numero solo admite [0-9.-] (1-20): un Guid en hexadecimal ("N") incluye letras a-f, inválidas
    // aquí. El contador arranca en un valor derivado del reloj para no colisionar entre corridas.
    private static long _contadorNumero = DateTime.UtcNow.Ticks % 1_000_000;

    private static string NuevoNumero() => $"9{Interlocked.Increment(ref _contadorNumero)}";

    private static async Task<PagedResult<CuentaContableResponse>> ListarPorNumeroAsync(HttpClient client, string numero) =>
        await ListarAsync(client, $"numero={numero}");

    private static async Task<PagedResult<CuentaContableResponse>> ListarAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/api/cuentas-contables?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PagedResult<CuentaContableResponse>>())!;
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

/// <summary>
/// Verifica la semilla del plan de cuentas en un contenedor Postgres propio, recién migrado, sin que
/// otros métodos de <see cref="CuentasContablesApiTests"/> (que crean/borran/modifican cuentas)
/// puedan haber alterado el estado: mismo motivo de aislamiento que <c>AlmacenesSemillaApiTests</c>.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CuentasContablesSemillaApiTests : IClassFixture<PostgresTestFixture>
{
    private readonly HttpClient _client;

    public CuentasContablesSemillaApiTests(PostgresTestFixture fixture)
    {
        var factory = fixture.CreateFactory();
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Theory]
    [InlineData("Activos", "1", "Activos", TipoCuentaContable.Encabezado, TipoResultadoCuenta.Balance, false, 0)]
    [InlineData("Caja", "1101", "Caja", TipoCuentaContable.Posteo, TipoResultadoCuenta.Balance, true, 1)]
    [InlineData("CxC", "1201", "Cuentas por cobrar clientes", TipoCuentaContable.Posteo, TipoResultadoCuenta.Balance, false, 1)]
    [InlineData("Inventario", "1301", "Inventario de mercancías", TipoCuentaContable.Posteo, TipoResultadoCuenta.Balance, false, 1)]
    [InlineData("Pasivos", "2", "Pasivos", TipoCuentaContable.Encabezado, TipoResultadoCuenta.Balance, false, 0)]
    [InlineData("IvaPorPagar", "2101", "ITBIS por pagar", TipoCuentaContable.Posteo, TipoResultadoCuenta.Balance, false, 1)]
    [InlineData("Ingresos", "4", "Ingresos", TipoCuentaContable.Encabezado, TipoResultadoCuenta.Resultado, false, 0)]
    [InlineData("Ventas", "4101", "Ventas", TipoCuentaContable.Posteo, TipoResultadoCuenta.Resultado, true, 1)]
    [InlineData("DescuentoVentas", "4102", "Descuentos sobre ventas", TipoCuentaContable.Posteo, TipoResultadoCuenta.Resultado, true, 1)]
    [InlineData("Costos", "5", "Costos", TipoCuentaContable.Encabezado, TipoResultadoCuenta.Resultado, false, 0)]
    [InlineData("CostoVentas", "5101", "Costo de ventas", TipoCuentaContable.Posteo, TipoResultadoCuenta.Resultado, false, 1)]
    [InlineData("AjusteInventario", "5201", "Ajustes de inventario", TipoCuentaContable.Posteo, TipoResultadoCuenta.Resultado, true, 1)]
    public async Task LaSemilla_ExisteTrasMigrar_ConLosValoresDelBrief(
        string idConstante, string numero, string nombre, TipoCuentaContable tipoCuenta, TipoResultadoCuenta tipoResultado,
        bool posteoDirecto, int sangria)
    {
        var client = _client;
        client.DefaultRequestHeaders.Add("X-Test-User", "administrador");
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Administrador");

        var id = IdPorConstante(idConstante);
        var response = await client.GetAsync($"/api/cuentas-contables/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cuenta = await response.Content.ReadFromJsonAsync<CuentaContableResponse>();
        Assert.Equal(numero, cuenta!.Numero);
        Assert.Equal(nombre, cuenta.Nombre);
        Assert.Equal(tipoCuenta, cuenta.TipoCuenta);
        Assert.Equal(tipoResultado, cuenta.TipoResultado);
        Assert.Equal(posteoDirecto, cuenta.PosteoDirecto);
        Assert.False(cuenta.Bloqueada);
        Assert.Equal(sangria, cuenta.Sangria);
    }

    [Fact]
    public async Task LaSemilla_TieneExactamenteDoceCuentas()
    {
        var client = _client;
        client.DefaultRequestHeaders.Add("X-Test-User", "administrador");
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Administrador");

        var lista = await client.GetAsync("/api/cuentas-contables?tamanoPagina=200");
        Assert.Equal(HttpStatusCode.OK, lista.StatusCode);
        var paged = await lista.Content.ReadFromJsonAsync<PagedResult<CuentaContableResponse>>();
        Assert.Equal(12, paged!.Total);

        // Orden por defecto: Numero ascendente.
        var numeros = paged.Items.Select(x => x.Numero).ToList();
        Assert.Equal(numeros.OrderBy(n => n, StringComparer.Ordinal), numeros);
    }

    private static Guid IdPorConstante(string nombre) => nombre switch
    {
        "Activos" => CuentaContableIds.Activos,
        "Caja" => CuentaContableIds.Caja,
        "CxC" => CuentaContableIds.CxC,
        "Inventario" => CuentaContableIds.Inventario,
        "Pasivos" => CuentaContableIds.Pasivos,
        "IvaPorPagar" => CuentaContableIds.IvaPorPagar,
        "Ingresos" => CuentaContableIds.Ingresos,
        "Ventas" => CuentaContableIds.Ventas,
        "DescuentoVentas" => CuentaContableIds.DescuentoVentas,
        "Costos" => CuentaContableIds.Costos,
        "CostoVentas" => CuentaContableIds.CostoVentas,
        "AjusteInventario" => CuentaContableIds.AjusteInventario,
        _ => throw new ArgumentOutOfRangeException(nameof(nombre))
    };
}
