using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenSource1.Application.Features.GruposContables.Dtos;
using OpenSource1.Application.Features.SetupsContables.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

/// <summary>
/// API de setups contables (Task 5.4): <c>api/setups-contables/{general|iva|inventario}</c>, sus validaciones, la semilla y las
/// guardas de borrado ampliadas (cuenta, grupo o almacén usado por un setup vivo → 409; Review Focus 4).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SetupsContablesApiTests : IClassFixture<PostgresTestFixture>
{
    private readonly HttpClient _client;

    public SetupsContablesApiTests(PostgresTestFixture fixture)
    {
        _client = fixture.CreateFactory().CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task General_Crud_ConComodin_Xmin_YGrupoYCuentaEnUsoNoSePuedenBorrar()
    {
        var client = CreateClient("Administrador");
        var negocio = await CrearGrupoAsync(client, "negocio");
        var producto = await CrearGrupoAsync(client, "producto");
        var ventas = await CrearCuentaAsync(client);
        var otra = await CrearCuentaAsync(client);

        var create = await client.PostAsJsonAsync("/api/setups-contables/general", new
        {
            grupoProductoId = producto.Id, cuentaVentasId = ventas, cuentaCostoVentasId = ventas, cuentaDescuentoVentasId = ventas, cuentaAjusteInventarioId = ventas
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var creado = (await create.Content.ReadFromJsonAsync<SetupGeneralResponse>())!;
        Assert.Null(creado.GrupoNegocioId);
        Assert.Null(creado.GrupoNegocioCodigo);
        Assert.Equal(producto.Codigo, creado.GrupoProductoCodigo);
        Assert.False(string.IsNullOrEmpty(creado.CuentaVentasNumero));
        Assert.True(creado.Xmin > 0);
        Assert.EndsWith($"/api/setups-contables/general/{creado.Id}", create.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);

        // El mismo Id no existe bajo otro tipo (otra tabla).
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/setups-contables/iva/{creado.Id}")).StatusCode);

        var lista = (await client.GetFromJsonAsync<PagedResult<SetupGeneralResponse>>($"/api/setups-contables/general?grupoProductoId={producto.Id}"))!;
        Assert.Equal(creado.Id, Assert.Single(lista.Items).Id);

        // Comodín duplicado -> 409 con los códigos.
        var duplicado = await client.PostAsJsonAsync("/api/setups-contables/general", new
        {
            grupoProductoId = producto.Id, cuentaVentasId = otra, cuentaCostoVentasId = otra, cuentaDescuentoVentasId = otra, cuentaAjusteInventarioId = otra
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);
        Assert.Contains($"GrupoProducto={producto.Codigo}", await duplicado.Content.ReadAsStringAsync());

        // Review Focus 4: la cuenta y los grupos usados por un setup vivo no se pueden borrar ni la cuenta dejar de ser de Posteo.
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/cuentas-contables/{ventas}")).StatusCode);
        var cuenta = (await client.GetFromJsonAsync<JsonDocument>($"/api/cuentas-contables/{ventas}"))!.RootElement;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/cuentas-contables/{ventas}", new
        {
            numero = cuenta.GetProperty("numero").GetString(), nombre = "X", tipoCuenta = TipoCuentaContable.Encabezado,
            tipoResultado = TipoResultadoCuenta.Balance, xmin = cuenta.GetProperty("xmin").GetInt64()
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/grupos-contables/producto/{producto.Id}")).StatusCode);

        // PUT: asigna el grupo de negocio (deja de ser comodín) y cambia las cuentas.
        var put = await client.PutAsJsonAsync($"/api/setups-contables/general/{creado.Id}", new
        {
            grupoNegocioId = negocio.Id, grupoProductoId = producto.Id, cuentaVentasId = otra, cuentaCostoVentasId = otra,
            cuentaDescuentoVentasId = otra, cuentaAjusteInventarioId = otra, xmin = creado.Xmin
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var modificado = (await put.Content.ReadFromJsonAsync<SetupGeneralResponse>())!;
        Assert.Equal(negocio.Codigo, modificado.GrupoNegocioCodigo);
        Assert.Equal(otra, modificado.CuentaVentasId);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/grupos-contables/negocio/{negocio.Id}")).StatusCode);
        // La cuenta que dejó de usarse ya se puede borrar.
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/cuentas-contables/{ventas}")).StatusCode);

        // Xmin viejo -> 409.
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/setups-contables/general/{creado.Id}", new
        {
            grupoProductoId = producto.Id, cuentaVentasId = otra, cuentaCostoVentasId = otra, cuentaDescuentoVentasId = otra,
            cuentaAjusteInventarioId = otra, xmin = creado.Xmin
        })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/setups-contables/general/{creado.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/setups-contables/general/{creado.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/setups-contables/general/{creado.Id}")).StatusCode);
        // Borrado el setup, grupos y cuenta quedan libres.
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/grupos-contables/negocio/{negocio.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/grupos-contables/producto/{producto.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/cuentas-contables/{otra}")).StatusCode);
    }

    [Theory]
    [InlineData("encabezado")]
    [InlineData("bloqueada")]
    [InlineData("inexistente")]
    public async Task General_CuentaNoValida_Devuelve400EnSuCampo(string caso)
    {
        var client = CreateClient("Administrador");
        var producto = await CrearGrupoAsync(client, "producto");
        var valida = await CrearCuentaAsync(client);
        var cuenta = caso switch
        {
            "encabezado" => CuentaContableIds.Ingresos,
            "bloqueada" => await CrearCuentaAsync(client, bloqueada: true),
            _ => Guid.NewGuid()
        };

        var response = await client.PostAsJsonAsync("/api/setups-contables/general", new
        {
            grupoProductoId = producto.Id, cuentaVentasId = valida, cuentaCostoVentasId = valida, cuentaDescuentoVentasId = cuenta, cuentaAjusteInventarioId = valida
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("errors").TryGetProperty("CuentaDescuentoVentasId", out _));
    }

    [Fact]
    public async Task General_SinGrupoProducto_O_ConGrupoInexistente_Devuelve400()
    {
        var client = CreateClient("Administrador");
        var cuenta = await CrearCuentaAsync(client);

        var sinProducto = await client.PostAsJsonAsync("/api/setups-contables/general", new
        {
            cuentaVentasId = cuenta, cuentaCostoVentasId = cuenta, cuentaDescuentoVentasId = cuenta, cuentaAjusteInventarioId = cuenta
        });
        Assert.Equal(HttpStatusCode.BadRequest, sinProducto.StatusCode);
        Assert.True((await sinProducto.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("errors").TryGetProperty("GrupoProductoId", out _));

        var grupoInexistente = await client.PostAsJsonAsync("/api/setups-contables/general", new
        {
            grupoNegocioId = Guid.NewGuid(), grupoProductoId = GrupoContableIds.ProductoBienes,
            cuentaVentasId = cuenta, cuentaCostoVentasId = cuenta, cuentaDescuentoVentasId = cuenta, cuentaAjusteInventarioId = cuenta
        });
        Assert.Equal(HttpStatusCode.BadRequest, grupoInexistente.StatusCode);
    }

    [Fact]
    public async Task Iva_Crud_Validaciones_YCuentaDeComprasOpcional()
    {
        var client = CreateClient("Administrador");
        var ivaNegocio = await CrearGrupoAsync(client, "iva-negocio");
        var ivaProducto = await CrearGrupoAsync(client, "iva-producto");
        var ventas = await CrearCuentaAsync(client);
        var compras = await CrearCuentaAsync(client);

        object Cuerpo(decimal porcentaje, TipoCalculoIva tipo) => new
        {
            grupoIvaNegocioId = ivaNegocio.Id, grupoIvaProductoId = ivaProducto.Id, porcentajeIva = porcentaje, cuentaIvaVentasId = ventas,
            cuentaIvaComprasId = compras, identificadorIva = "iva-prueba", tipoCalculoIva = tipo
        };

        foreach (var (porcentaje, tipo) in new[] { (100.5m, TipoCalculoIva.Normal), (18.123456m, TipoCalculoIva.Normal), (18m, TipoCalculoIva.Exento) })
        {
            var invalido = await client.PostAsJsonAsync("/api/setups-contables/iva", Cuerpo(porcentaje, tipo));
            Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);
            Assert.True((await invalido.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("errors").TryGetProperty("PorcentajeIva", out _));
        }

        var create = await client.PostAsJsonAsync("/api/setups-contables/iva", Cuerpo(16.5m, TipoCalculoIva.Normal));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var creado = (await create.Content.ReadFromJsonAsync<SetupIvaResponse>())!;
        Assert.Equal(16.5m, creado.PorcentajeIva);
        Assert.Equal("IVA-PRUEBA", creado.IdentificadorIva);
        Assert.Equal(TipoCalculoIva.Normal, creado.TipoCalculoIva);
        Assert.Equal(ivaNegocio.Codigo, creado.GrupoIvaNegocioCodigo);
        Assert.Equal(compras, creado.CuentaIvaComprasId);
        Assert.False(string.IsNullOrEmpty(creado.CuentaIvaComprasNumero));

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/setups-contables/iva", Cuerpo(0m, TipoCalculoIva.Exento))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/cuentas-contables/{compras}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/grupos-contables/iva-producto/{ivaProducto.Id}")).StatusCode);

        // PUT sin cuentaIvaComprasId = conservar; Guid vacío = quitar (y entonces esa cuenta se puede borrar).
        var put = await client.PutAsJsonAsync($"/api/setups-contables/iva/{creado.Id}", new
        {
            grupoIvaNegocioId = ivaNegocio.Id, grupoIvaProductoId = ivaProducto.Id, porcentajeIva = 0m, cuentaIvaVentasId = ventas,
            identificadorIva = "EXENTO", tipoCalculoIva = TipoCalculoIva.Exento, xmin = creado.Xmin
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var modificado = (await put.Content.ReadFromJsonAsync<SetupIvaResponse>())!;
        Assert.Equal(compras, modificado.CuentaIvaComprasId);
        Assert.Equal(TipoCalculoIva.Exento, modificado.TipoCalculoIva);

        put = await client.PutAsJsonAsync($"/api/setups-contables/iva/{creado.Id}", new
        {
            grupoIvaNegocioId = ivaNegocio.Id, grupoIvaProductoId = ivaProducto.Id, porcentajeIva = 0m, cuentaIvaVentasId = ventas,
            cuentaIvaComprasId = Guid.Empty, identificadorIva = "EXENTO", tipoCalculoIva = TipoCalculoIva.Exento, xmin = modificado.Xmin
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Null((await put.Content.ReadFromJsonAsync<SetupIvaResponse>())!.CuentaIvaComprasId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/cuentas-contables/{compras}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/setups-contables/iva/{creado.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/grupos-contables/iva-producto/{ivaProducto.Id}")).StatusCode);
    }

    [Fact]
    public async Task Inventario_Crud_ComodinYAlmacen_YElAlmacenUsadoNoSePuedeBorrar()
    {
        var client = CreateClient("Administrador");
        var grupo = await CrearGrupoAsync(client, "inventario");
        var cuenta = await CrearCuentaAsync(client);
        var almacen = await CrearAlmacenAsync(client);

        object Cuerpo(Guid? almacenId) => new
        {
            almacenId, grupoInventarioId = grupo.Id, cuentaInventarioId = cuenta, cuentaAjusteInventarioId = cuenta, cuentaVariacionCostoId = cuenta
        };

        var comodin = await client.PostAsJsonAsync("/api/setups-contables/inventario", Cuerpo(null));
        Assert.Equal(HttpStatusCode.Created, comodin.StatusCode);
        var exacto = await client.PostAsJsonAsync("/api/setups-contables/inventario", Cuerpo(almacen));
        Assert.Equal(HttpStatusCode.Created, exacto.StatusCode);
        var fila = (await exacto.Content.ReadFromJsonAsync<SetupInventarioResponse>())!;
        Assert.Equal(almacen, fila.AlmacenId);
        Assert.False(string.IsNullOrEmpty(fila.AlmacenCodigo));

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/setups-contables/inventario", Cuerpo(almacen))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/setups-contables/inventario", Cuerpo(null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/setups-contables/inventario", Cuerpo(Guid.NewGuid()))).StatusCode);

        // Listado del grupo: el comodín primero.
        var lista = (await client.GetFromJsonAsync<PagedResult<SetupInventarioResponse>>($"/api/setups-contables/inventario?grupoInventarioId={grupo.Id}"))!;
        Assert.Equal(2, lista.Items.Count);
        Assert.Null(lista.Items[0].AlmacenId);
        Assert.Equal(almacen, lista.Items[1].AlmacenId);
        Assert.Equal(fila.Id, Assert.Single((await client.GetFromJsonAsync<PagedResult<SetupInventarioResponse>>($"/api/setups-contables/inventario?almacenId={almacen}"))!.Items).Id);

        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/almacenes/{almacen}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/grupos-contables/inventario/{grupo.Id}")).StatusCode);

        // PUT: pasar la fila del almacén a comodín choca con el comodín existente.
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/setups-contables/inventario/{fila.Id}", new
        {
            almacenId = (Guid?)null, grupoInventarioId = grupo.Id, cuentaInventarioId = cuenta, cuentaAjusteInventarioId = cuenta,
            cuentaVariacionCostoId = cuenta, xmin = fila.Xmin
        })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/setups-contables/inventario/{fila.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/almacenes/{almacen}")).StatusCode);
    }

    [Fact]
    public async Task Roles_Y_TipoDesconocido()
    {
        var anon = new HttpRequestMessage(HttpMethod.Get, "/api/setups-contables/general");
        anon.Headers.Add("X-Test-Anonymous", "true");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(anon)).StatusCode);

        var supervisor = CreateClient("Supervisor");
        Assert.Equal(HttpStatusCode.OK, (await supervisor.GetAsync("/api/setups-contables/iva")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await supervisor.PostAsJsonAsync("/api/setups-contables/inventario", new { grupoInventarioId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Ejecutor").DeleteAsync($"/api/setups-contables/general/{Guid.NewGuid()}")).StatusCode);

        var admin = CreateClient("Administrador");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/setups-contables/xyz")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/setups-contables/general/{Guid.NewGuid()}", new
        {
            grupoProductoId = GrupoContableIds.ProductoBienes, cuentaVentasId = CuentaContableIds.Ventas, cuentaCostoVentasId = CuentaContableIds.Ventas,
            cuentaDescuentoVentasId = CuentaContableIds.Ventas, cuentaAjusteInventarioId = CuentaContableIds.Ventas, xmin = 1
        })).StatusCode);
    }

    [Fact]
    public async Task LaSemilla_ExisteTrasMigrar_YProtegeSusCuentasYGrupos()
    {
        var client = CreateClient("Administrador");

        var bienes = (await client.GetFromJsonAsync<PagedResult<SetupGeneralResponse>>($"/api/setups-contables/general?grupoProductoId={GrupoContableIds.ProductoBienes}"))!;
        Assert.Equal(["(comodín)", "NACIONAL"], bienes.Items.Select(x => x.GrupoNegocioCodigo ?? "(comodín)"));
        Assert.All(bienes.Items, x =>
        {
            Assert.Equal(("4101", "5101", "4102", "5201"), (x.CuentaVentasNumero, x.CuentaCostoVentasNumero, x.CuentaDescuentoVentasNumero, x.CuentaAjusteInventarioNumero));
        });
        Assert.Equal(2, (await client.GetFromJsonAsync<PagedResult<SetupGeneralResponse>>($"/api/setups-contables/general?grupoProductoId={GrupoContableIds.ProductoServicios}"))!.Total);

        var itbis = (await client.GetFromJsonAsync<SetupIvaResponse>($"/api/setups-contables/iva/{SetupContableIds.IvaItbis18Itbis18}"))!;
        Assert.Equal((18m, "2101", "ITBIS18", TipoCalculoIva.Normal, "ITBIS18", "ITBIS18"),
            (itbis.PorcentajeIva, itbis.CuentaIvaVentasNumero, itbis.IdentificadorIva, itbis.TipoCalculoIva, itbis.GrupoIvaNegocioCodigo, itbis.GrupoIvaProductoCodigo));
        foreach (var id in new[] { SetupContableIds.IvaItbis18Exento, SetupContableIds.IvaExentoItbis18, SetupContableIds.IvaExentoExento })
        {
            var exento = (await client.GetFromJsonAsync<SetupIvaResponse>($"/api/setups-contables/iva/{id}"))!;
            Assert.Equal((0m, "2101", "EXENTO", TipoCalculoIva.Exento), (exento.PorcentajeIva, exento.CuentaIvaVentasNumero, exento.IdentificadorIva, exento.TipoCalculoIva));
        }

        var inventario = (await client.GetFromJsonAsync<SetupInventarioResponse>($"/api/setups-contables/inventario/{SetupContableIds.InventarioCualquieraGeneral}"))!;
        Assert.Null(inventario.AlmacenId);
        Assert.Equal(("GENERAL", "1301", "5201", "5201"),
            (inventario.GrupoInventarioCodigo, inventario.CuentaInventarioNumero, inventario.CuentaAjusteInventarioNumero, inventario.CuentaVariacionCostoNumero));

        // Las cuentas y grupos de la semilla quedan protegidos por los setups.
        foreach (var cuenta in new[] { CuentaContableIds.Ventas, CuentaContableIds.CostoVentas, CuentaContableIds.Inventario, CuentaContableIds.IvaPorPagar, CuentaContableIds.AjusteInventario })
        {
            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/cuentas-contables/{cuenta}")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/grupos-contables/negocio/{GrupoContableIds.NegocioNacional}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/grupos-contables/iva-negocio/{GrupoContableIds.IvaNegocioExento}")).StatusCode);
    }

    // ── Ayudas ──

    private static int _contador = (int)(DateTime.UtcNow.Ticks % 100_000);
    private static long _contadorCuenta = DateTime.UtcNow.Ticks % 1_000_000;

    private static async Task<GrupoContableResponse> CrearGrupoAsync(HttpClient client, string ruta)
    {
        var response = await client.PostAsJsonAsync($"/api/grupos-contables/{ruta}", new { codigo = $"S{Interlocked.Increment(ref _contador)}", descripcion = $"Grupo {ruta}" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GrupoContableResponse>())!;
    }

    private static async Task<Guid> CrearCuentaAsync(HttpClient client, bool bloqueada = false)
    {
        var response = await client.PostAsJsonAsync("/api/cuentas-contables", new
        {
            numero = $"7{Interlocked.Increment(ref _contadorCuenta)}", nombre = "Cuenta de setup", tipoCuenta = TipoCuentaContable.Posteo,
            tipoResultado = TipoResultadoCuenta.Resultado, posteoDirecto = false, bloqueada, sangria = 1
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CrearAlmacenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/almacenes", new { codigo = $"S{Interlocked.Increment(ref _contador)}", nombre = "Almacén de setup" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
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
