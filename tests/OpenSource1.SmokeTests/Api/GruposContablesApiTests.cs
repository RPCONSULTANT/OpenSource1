using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenSource1.Application.Features.GruposClienteContable.Dtos;
using OpenSource1.Application.Features.GruposContables.Dtos;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

/// <summary>
/// API de grupos contables (Task 5.3): el mantenimiento genérico <c>api/grupos-contables/{tipo}</c>, el de grupos de cliente
/// <c>api/grupos-cliente-contable</c>, la clasificación contable en productos y socios, y las guardas de borrado en uso.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class GruposContablesApiTests : IClassFixture<PostgresTestFixture>
{
    private readonly HttpClient _client;

    public GruposContablesApiTests(PostgresTestFixture fixture)
    {
        var factory = fixture.CreateFactory();
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Theory]
    [InlineData("negocio", TipoGrupoContable.Negocio)]
    [InlineData("producto", TipoGrupoContable.Producto)]
    [InlineData("iva-negocio", TipoGrupoContable.IvaNegocio)]
    [InlineData("iva-producto", TipoGrupoContable.IvaProducto)]
    [InlineData("inventario", TipoGrupoContable.Inventario)]
    public async Task Crud_PorTipo_EnSuPropiaTabla(string ruta, TipoGrupoContable tipo)
    {
        var client = CreateClient("Administrador");
        var codigo = NuevoCodigo();

        var create = await client.PostAsJsonAsync($"/api/grupos-contables/{ruta}", new { codigo = codigo.ToLowerInvariant(), descripcion = "Grupo de prueba" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var creado = (await create.Content.ReadFromJsonAsync<GrupoContableResponse>())!;
        Assert.Equal(tipo, creado.Tipo);
        Assert.Equal(codigo, creado.Codigo);
        Assert.True(creado.Xmin > 0);
        Assert.EndsWith($"/api/grupos-contables/{ruta}/{creado.Id}", create.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);

        var get = await client.GetAsync($"/api/grupos-contables/{ruta}/{creado.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        // Cada tipo es su propia tabla: el mismo Id no existe bajo los otros tipos.
        var otraRuta = ruta == "negocio" ? "producto" : "negocio";
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/grupos-contables/{otraRuta}/{creado.Id}")).StatusCode);

        var lista = await ListarAsync(client, ruta, $"codigo={codigo}");
        Assert.Single(lista.Items, x => x.Id == creado.Id);

        var update = await client.PutAsJsonAsync($"/api/grupos-contables/{ruta}/{creado.Id}", new { codigo, descripcion = "Renombrado", xmin = creado.Xmin });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var modificado = (await update.Content.ReadFromJsonAsync<GrupoContableResponse>())!;
        Assert.Equal("Renombrado", modificado.Descripcion);

        // Xmin viejo -> 409.
        var conflicto = await client.PutAsJsonAsync($"/api/grupos-contables/{ruta}/{creado.Id}", new { codigo, descripcion = "Otra", xmin = creado.Xmin });
        Assert.Equal(HttpStatusCode.Conflict, conflicto.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/grupos-contables/{ruta}/{creado.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/grupos-contables/{ruta}/{creado.Id}")).StatusCode);
    }

    [Fact]
    public async Task TipoDesconocido_Devuelve404_EnTodasLasOperaciones()
    {
        var client = CreateClient("Administrador");

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/grupos-contables/cliente")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/grupos-contables/xyz/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/grupos-contables/xyz", new { codigo = "A", descripcion = "B" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/grupos-contables/xyz/{Guid.NewGuid()}", new { codigo = "A", descripcion = "B", xmin = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/grupos-contables/xyz/{Guid.NewGuid()}")).StatusCode);
        // Un número de enum tampoco es un nombre de tipo válido.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/grupos-contables/1")).StatusCode);
    }

    [Fact]
    public async Task Roles_CodigoDuplicado409_Y_CodigoInvalido400()
    {
        var anon = new HttpRequestMessage(HttpMethod.Get, "/api/grupos-contables/negocio");
        anon.Headers.Add("X-Test-Anonymous", "true");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(anon)).StatusCode);

        var supervisor = CreateClient("Supervisor");
        Assert.Equal(HttpStatusCode.OK, (await supervisor.GetAsync("/api/grupos-contables/negocio")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await supervisor.PostAsJsonAsync("/api/grupos-contables/negocio", new { codigo = NuevoCodigo(), descripcion = "X" })).StatusCode);

        var client = CreateClient("Administrador");
        var codigo = NuevoCodigo();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/grupos-contables/negocio", new { codigo, descripcion = "Uno" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/grupos-contables/negocio", new { codigo, descripcion = "Dos" })).StatusCode);
        // El mismo código en OTRO tipo es otra tabla: se permite.
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/grupos-contables/iva-negocio", new { codigo, descripcion = "Otro tipo" })).StatusCode);

        var invalido = await client.PostAsJsonAsync("/api/grupos-contables/negocio", new { codigo = "con espacio", descripcion = "X" });
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);
        var problema = await invalido.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.True(problema!.RootElement.GetProperty("errors").TryGetProperty("Codigo", out _));

        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Ejecutor").DeleteAsync($"/api/grupos-contables/negocio/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Producto_ConGrupos_LosDevuelve_ElPutSinGruposLosConserva_YBorrarUnGrupoUsadoDa409()
    {
        var client = CreateClient("Administrador");
        var grupoProducto = await CrearGrupoAsync(client, "producto");
        var grupoIva = await CrearGrupoAsync(client, "iva-producto");
        var grupoInventario = await CrearGrupoAsync(client, "inventario");

        var codigo = $"P-{Guid.NewGuid():N}";
        var create = await client.PostAsJsonAsync("/api/productos", new
        {
            codigo, nombre = "Con grupos", precioVenta = 1m,
            grupoProductoId = grupoProducto.Id, grupoIvaProductoId = grupoIva.Id, grupoInventarioId = grupoInventario.Id
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var producto = (await create.Content.ReadFromJsonAsync<ProductoResponse>())!;
        Assert.Equal(grupoProducto.Codigo, producto.GrupoProductoCodigo);

        var leido = (await client.GetFromJsonAsync<ProductoResponse>($"/api/productos/{producto.Id}"))!;
        Assert.Equal(grupoProducto.Id, leido.GrupoProductoId);
        Assert.Equal(grupoProducto.Codigo, leido.GrupoProductoCodigo);
        Assert.Equal(grupoIva.Codigo, leido.GrupoIvaProductoCodigo);
        Assert.Equal(grupoInventario.Codigo, leido.GrupoInventarioCodigo);

        // El listado también trae los códigos (y la subconsulta aplanada no rompe el filtro por código).
        var lista = (await client.GetFromJsonAsync<PagedResult<ProductoResponse>>($"/api/productos?codigo={codigo}"))!;
        Assert.Equal(grupoInventario.Codigo, Assert.Single(lista.Items).GrupoInventarioCodigo);

        // PUT sin grupos: null = conservar.
        var put = await client.PutAsJsonAsync($"/api/productos/{producto.Id}", new { codigo, nombre = "Renombrado" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        leido = (await client.GetFromJsonAsync<ProductoResponse>($"/api/productos/{producto.Id}"))!;
        Assert.Equal("Renombrado", leido.Nombre);
        Assert.Equal(grupoProducto.Id, leido.GrupoProductoId);
        Assert.Equal(grupoIva.Id, leido.GrupoIvaProductoId);
        Assert.Equal(grupoInventario.Id, leido.GrupoInventarioId);

        // PUT con un grupo inexistente: 400 grupo_invalido en su campo (no 404).
        var invalido = await client.PutAsJsonAsync($"/api/productos/{producto.Id}", new { codigo, nombre = "Renombrado", grupoIvaProductoId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);
        Assert.True((await invalido.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("errors").TryGetProperty("GrupoIvaProductoId", out _));

        // Borrar un grupo usado por un producto vivo -> 409.
        foreach (var (ruta, id) in new[] { ("producto", grupoProducto.Id), ("iva-producto", grupoIva.Id), ("inventario", grupoInventario.Id) })
        {
            var borrar = await client.DeleteAsync($"/api/grupos-contables/{ruta}/{id}");
            Assert.Equal(HttpStatusCode.Conflict, borrar.StatusCode);
        }

        // Con el producto borrado (lógicamente) el grupo ya no está en uso.
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/productos/{producto.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/grupos-contables/producto/{grupoProducto.Id}")).StatusCode);
    }

    [Fact]
    public async Task Socio_ConGrupos_LosDevuelve_ElPutSinGruposLosConserva_YBorrarUnGrupoUsadoDa409()
    {
        var client = CreateClient("Administrador");
        var grupoNegocio = await CrearGrupoAsync(client, "negocio");
        var grupoIva = await CrearGrupoAsync(client, "iva-negocio");
        var grupoCliente = await CrearGrupoClienteAsync(client, CuentaContableIds.CxC);

        var create = await client.PostAsJsonAsync("/api/socios-negocio", new
        {
            nombreComercial = "Socio con grupos",
            grupoNegocioId = grupoNegocio.Id, grupoIvaNegocioId = grupoIva.Id, grupoClienteContableId = grupoCliente.Id
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var socio = (await create.Content.ReadFromJsonAsync<SocioNegocioResponse>())!;
        Assert.Equal(grupoCliente.Codigo, socio.GrupoClienteContableCodigo);

        var leido = (await client.GetFromJsonAsync<SocioNegocioResponse>($"/api/socios-negocio/{socio.Id}"))!;
        Assert.Equal(grupoNegocio.Codigo, leido.GrupoNegocioCodigo);
        Assert.Equal(grupoIva.Codigo, leido.GrupoIvaNegocioCodigo);
        Assert.Equal(grupoCliente.Codigo, leido.GrupoClienteContableCodigo);

        var lista = (await client.GetFromJsonAsync<PagedResult<SocioNegocioResponse>>($"/api/socios-negocio?codigo={socio.Codigo}"))!;
        Assert.Equal(grupoNegocio.Codigo, Assert.Single(lista.Items).GrupoNegocioCodigo);

        var put = await client.PutAsJsonAsync($"/api/socios-negocio/{socio.Id}", new { nombreComercial = "Renombrado" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        leido = (await client.GetFromJsonAsync<SocioNegocioResponse>($"/api/socios-negocio/{socio.Id}"))!;
        Assert.Equal(grupoNegocio.Id, leido.GrupoNegocioId);
        Assert.Equal(grupoIva.Id, leido.GrupoIvaNegocioId);
        Assert.Equal(grupoCliente.Id, leido.GrupoClienteContableId);

        var invalido = await client.PutAsJsonAsync($"/api/socios-negocio/{socio.Id}", new { nombreComercial = "Renombrado", grupoNegocioId = Guid.Empty });
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/grupos-contables/negocio/{grupoNegocio.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/grupos-contables/iva-negocio/{grupoIva.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/grupos-cliente-contable/{grupoCliente.Id}")).StatusCode);
    }

    [Fact]
    public async Task GrupoCliente_Crud_ValidaLasCuentas_YUnaCuentaUsadaNoSePuedeBorrar()
    {
        var client = CreateClient("Administrador");

        // Cuenta de encabezado (1 Activos) como CxC -> 400 cuenta_invalida en su campo.
        var encabezado = await client.PostAsJsonAsync("/api/grupos-cliente-contable",
            new { codigo = NuevoCodigo(), descripcion = "X", cuentaCxCId = CuentaContableIds.Activos });
        Assert.Equal(HttpStatusCode.BadRequest, encabezado.StatusCode);
        Assert.True((await encabezado.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("errors").TryGetProperty("CuentaCxCId", out _));

        // Cuenta bloqueada -> 400.
        var bloqueada = await CrearCuentaAsync(client, bloqueada: true);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/grupos-cliente-contable",
            new { codigo = NuevoCodigo(), descripcion = "X", cuentaCxCId = bloqueada })).StatusCode);

        // Cuenta inexistente -> 400 (no 404).
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/grupos-cliente-contable",
            new { codigo = NuevoCodigo(), descripcion = "X", cuentaCxCId = Guid.NewGuid() })).StatusCode);

        var cxc = await CrearCuentaAsync(client);
        var descuento = await CrearCuentaAsync(client);
        var create = await client.PostAsJsonAsync("/api/grupos-cliente-contable",
            new { codigo = NuevoCodigo(), descripcion = "Mayoristas", cuentaCxCId = cxc, cuentaDescuentoId = descuento });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var grupo = (await create.Content.ReadFromJsonAsync<GrupoClienteContableResponse>())!;
        Assert.Equal(descuento, grupo.CuentaDescuentoId);
        Assert.False(string.IsNullOrEmpty(grupo.CuentaCxCNumero));

        var leido = (await client.GetFromJsonAsync<GrupoClienteContableResponse>($"/api/grupos-cliente-contable/{grupo.Id}"))!;
        Assert.Equal(grupo.CuentaCxCNumero, leido.CuentaCxCNumero);
        Assert.NotNull(leido.CuentaDescuentoNumero);
        Assert.Contains((await client.GetFromJsonAsync<PagedResult<GrupoClienteContableResponse>>($"/api/grupos-cliente-contable?codigo={grupo.Codigo}"))!.Items, x => x.Id == grupo.Id);

        // Las cuentas usadas por el grupo no se pueden borrar (guarda de uso de cuentas ampliada en la 5.3).
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/cuentas-contables/{cxc}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/cuentas-contables/{descuento}")).StatusCode);

        // PUT: descuento ausente = conservar.
        var put = await client.PutAsJsonAsync($"/api/grupos-cliente-contable/{grupo.Id}",
            new { codigo = grupo.Codigo, descripcion = "Renombrado", cuentaCxCId = cxc, xmin = grupo.Xmin });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var modificado = (await put.Content.ReadFromJsonAsync<GrupoClienteContableResponse>())!;
        Assert.Equal(descuento, modificado.CuentaDescuentoId);

        // PUT: Guid vacío = quitar el descuento; entonces esa cuenta ya se puede borrar.
        put = await client.PutAsJsonAsync($"/api/grupos-cliente-contable/{grupo.Id}",
            new { codigo = grupo.Codigo, descripcion = "Renombrado", cuentaCxCId = cxc, cuentaDescuentoId = Guid.Empty, xmin = modificado.Xmin });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Null((await put.Content.ReadFromJsonAsync<GrupoClienteContableResponse>())!.CuentaDescuentoId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/cuentas-contables/{descuento}")).StatusCode);

        // Xmin viejo -> 409.
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/grupos-cliente-contable/{grupo.Id}",
            new { codigo = grupo.Codigo, descripcion = "Otra", cuentaCxCId = cxc, xmin = grupo.Xmin })).StatusCode);

        // Sin uso: se borra, y con él la cuenta CxC queda libre.
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/grupos-cliente-contable/{grupo.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/grupos-cliente-contable/{grupo.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/cuentas-contables/{cxc}")).StatusCode);
    }

    private static int _contador = (int)(DateTime.UtcNow.Ticks % 100_000);

    private static string NuevoCodigo() => $"T{Interlocked.Increment(ref _contador)}";

    private static long _contadorCuenta = DateTime.UtcNow.Ticks % 1_000_000;

    private static async Task<GrupoContableResponse> CrearGrupoAsync(HttpClient client, string ruta)
    {
        var response = await client.PostAsJsonAsync($"/api/grupos-contables/{ruta}", new { codigo = NuevoCodigo(), descripcion = $"Grupo {ruta}" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GrupoContableResponse>())!;
    }

    private static async Task<GrupoClienteContableResponse> CrearGrupoClienteAsync(HttpClient client, Guid cuentaCxCId)
    {
        var response = await client.PostAsJsonAsync("/api/grupos-cliente-contable", new { codigo = NuevoCodigo(), descripcion = "Grupo cliente", cuentaCxCId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GrupoClienteContableResponse>())!;
    }

    private static async Task<Guid> CrearCuentaAsync(HttpClient client, bool bloqueada = false)
    {
        var response = await client.PostAsJsonAsync("/api/cuentas-contables", new
        {
            numero = $"8{Interlocked.Increment(ref _contadorCuenta)}", nombre = "Cuenta de prueba", tipoCuenta = TipoCuentaContable.Posteo,
            tipoResultado = TipoResultadoCuenta.Balance, posteoDirecto = true, bloqueada, sangria = 1
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<PagedResult<GrupoContableResponse>> ListarAsync(HttpClient client, string ruta, string query)
    {
        var response = await client.GetAsync($"/api/grupos-contables/{ruta}?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PagedResult<GrupoContableResponse>>())!;
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
/// Semilla de grupos en un contenedor recién migrado (sin que otras pruebas la alteren), y la guarda de uso de cuentas sobre
/// la semilla: la cuenta 1201 (CxC del grupo de cliente GENERAL) y la 4102 (su descuento) no se pueden borrar ni dejar de ser
/// de Posteo.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class GruposContablesSemillaApiTests : IClassFixture<PostgresTestFixture>
{
    private readonly HttpClient _client;

    public GruposContablesSemillaApiTests(PostgresTestFixture fixture)
    {
        _client = fixture.CreateFactory().CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        _client.DefaultRequestHeaders.Add("X-Test-User", "administrador");
        _client.DefaultRequestHeaders.Add("X-Test-Roles", "Administrador");
    }

    [Theory]
    [InlineData("negocio", "EXTERIOR,NACIONAL")]
    [InlineData("producto", "BIENES,SERVICIOS")]
    [InlineData("iva-negocio", "EXENTO,ITBIS18")]
    [InlineData("iva-producto", "EXENTO,ITBIS18")]
    [InlineData("inventario", "GENERAL")]
    public async Task LaSemilla_ExisteTrasMigrar_OrdenadaPorCodigo(string ruta, string codigos)
    {
        var lista = (await _client.GetFromJsonAsync<PagedResult<GrupoContableResponse>>($"/api/grupos-contables/{ruta}"))!;
        Assert.Equal(codigos, string.Join(",", lista.Items.Select(x => x.Codigo)));
    }

    [Theory]
    [InlineData("negocio", "f2000000-0000-0000-0000-000000000001", "NACIONAL")]
    [InlineData("negocio", "f2000000-0000-0000-0000-000000000002", "EXTERIOR")]
    [InlineData("producto", "f2000000-0000-0000-0000-000000000003", "BIENES")]
    [InlineData("producto", "f2000000-0000-0000-0000-000000000004", "SERVICIOS")]
    [InlineData("iva-negocio", "f2000000-0000-0000-0000-000000000005", "ITBIS18")]
    [InlineData("iva-negocio", "f2000000-0000-0000-0000-000000000006", "EXENTO")]
    [InlineData("iva-producto", "f2000000-0000-0000-0000-000000000007", "ITBIS18")]
    [InlineData("iva-producto", "f2000000-0000-0000-0000-000000000008", "EXENTO")]
    [InlineData("inventario", "f2000000-0000-0000-0000-000000000009", "GENERAL")]
    public async Task LaSemilla_TieneLosIdsFijos(string ruta, string id, string codigo)
    {
        var grupo = (await _client.GetFromJsonAsync<GrupoContableResponse>($"/api/grupos-contables/{ruta}/{id}"))!;
        Assert.Equal(codigo, grupo.Codigo);
    }

    [Fact]
    public async Task ElGrupoDeClienteGeneral_ApuntaA1201Y4102_YEsasCuentasNoSePuedenBorrarNiDejarDeSerDePosteo()
    {
        var general = (await _client.GetFromJsonAsync<GrupoClienteContableResponse>(
            $"/api/grupos-cliente-contable/{GrupoContableIds.ClienteContableGeneral}"))!;
        Assert.Equal("GENERAL", general.Codigo);
        Assert.Equal(CuentaContableIds.CxC, general.CuentaCxCId);
        Assert.Equal("1201", general.CuentaCxCNumero);
        Assert.Equal(CuentaContableIds.DescuentoVentas, general.CuentaDescuentoId);
        Assert.Equal("4102", general.CuentaDescuentoNumero);
        Assert.Null(general.CuentaInteresId);

        Assert.Equal(HttpStatusCode.Conflict, (await _client.DeleteAsync($"/api/cuentas-contables/{CuentaContableIds.CxC}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.DeleteAsync($"/api/cuentas-contables/{CuentaContableIds.DescuentoVentas}")).StatusCode);

        var cuenta = (await _client.GetFromJsonAsync<JsonDocument>($"/api/cuentas-contables/{CuentaContableIds.CxC}"))!.RootElement;
        var cambioTipo = await _client.PutAsJsonAsync($"/api/cuentas-contables/{CuentaContableIds.CxC}", new
        {
            numero = "1201", nombre = "Cuentas por cobrar clientes", tipoCuenta = TipoCuentaContable.Encabezado,
            tipoResultado = TipoResultadoCuenta.Balance, xmin = cuenta.GetProperty("xmin").GetInt64()
        });
        Assert.Equal(HttpStatusCode.Conflict, cambioTipo.StatusCode);

        // Una cuenta de la semilla que nadie usa (1101 Caja) sí se puede borrar.
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/cuentas-contables/{CuentaContableIds.Caja}")).StatusCode);
    }
}
