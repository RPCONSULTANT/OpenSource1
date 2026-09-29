using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using OpenSource1.Application.Features.FacturasVenta.Borradores.Dtos;
using OpenSource1.Application.Features.MovimientosCliente.Dtos;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

/// <summary>
/// <c>POST api/cobros</c>, <c>POST api/cobros/aplicaciones</c> y <c>GET api/clientes/{id}/movimientos-abiertos</c> (Task 6.5) contra
/// Postgres real: permisos (CanModify / CanConsult), forma de las respuestas, 400 con el campo y 404 del socio de la ruta.
/// REQUIERE DOCKER. Los casos del motor están en <c>CobrosTests</c>.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CobrosApiTests : IClassFixture<PostgresTestFixture>
{
    private static readonly DateOnly D10 = new(2026, 9, 10);

    private readonly HttpClient _client;
    private readonly PostgresTestFixture _fixture;

    public CobrosApiTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateFactory().CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Factura118_Pago50_Aplicacion50_PorLaApi_MovimientosAbiertosYSaldo68()
    {
        var socio = await CrearSocioAsync(Admin(), "Cliente cobros API");
        await PostearFacturaAsync(Admin(), socio, 100m);

        // Pago: Ejecutor 403, anónimo 401; Supervisor (CanModify) 200 con exactamente numero/movimientoClienteId/registroContable.
        var cuerpoPago = new { socioNegocioId = socio, importe = 50m, fechaRegistro = D10, descripcion = "Efectivo" };
        Assert.Equal(HttpStatusCode.Forbidden, (await Rol("Ejecutor").PostAsJsonAsync("/api/cobros", cuerpoPago)).StatusCode);
        var anon = new HttpRequestMessage(HttpMethod.Post, "/api/cobros") { Content = JsonContent.Create(cuerpoPago) };
        anon.Headers.Add("X-Test-Anonymous", "true");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(anon)).StatusCode);

        var respuesta = await Rol("Supervisor").PostAsJsonAsync("/api/cobros", cuerpoPago);

        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, cuerpo);
        var pago = JsonDocument.Parse(cuerpo).RootElement;
        Assert.Equal(["numero", "movimientoClienteId", "registroContable"], pago.EnumerateObject().Select(p => p.Name));
        var pagoId = pago.GetProperty("movimientoClienteId").GetInt64();
        Assert.Equal(8, pago.GetProperty("numero").GetString()!.Length);
        Assert.False(string.IsNullOrEmpty(pago.GetProperty("registroContable").GetString()));

        // Movimientos abiertos (Ejecutor puede consultar): la factura (118) y el pago (−50).
        var abiertos = (await Rol("Ejecutor").GetFromJsonAsync<List<MovimientoClienteResponse>>($"/api/clientes/{socio}/movimientos-abiertos"))!;
        Assert.Equal([(TipoDocumentoCliente.Factura, 118m), (TipoDocumentoCliente.Pago, -50m)], abiertos.Select(m => (m.TipoDocumento, m.ImporteRestante)));
        var facturaId = abiertos[0].Id;

        // Aplicación: Ejecutor 403; 400 con el campo al exceder; 200 con los restantes.
        Assert.Equal(HttpStatusCode.Forbidden, (await Rol("Ejecutor").PostAsJsonAsync(
            "/api/cobros/aplicaciones", new { movimientoFacturaId = facturaId, movimientoPagoId = pagoId, importe = 50m })).StatusCode);
        await AssertErrorAsync(
            await Admin().PostAsJsonAsync("/api/cobros/aplicaciones", new { movimientoFacturaId = facturaId, movimientoPagoId = pagoId, importe = 60m }),
            HttpStatusCode.BadRequest, "Importe", "excede");

        var aplicada = await Rol("Supervisor").PostAsJsonAsync(
            "/api/cobros/aplicaciones", new { movimientoFacturaId = facturaId, movimientoPagoId = pagoId, importe = 50m, fechaRegistro = D10 });

        var cuerpoAplicada = await aplicada.Content.ReadAsStringAsync();
        Assert.True(aplicada.StatusCode == HttpStatusCode.OK, cuerpoAplicada);
        var aplicacion = JsonDocument.Parse(cuerpoAplicada).RootElement;
        Assert.Equal((68m, 0m), (aplicacion.GetProperty("restanteFactura").GetDecimal(), aplicacion.GetProperty("restantePago").GetDecimal()));

        var restantes = (await Admin().GetFromJsonAsync<List<MovimientoClienteResponse>>($"/api/clientes/{socio}/movimientos-abiertos"))!;
        Assert.Equal([(facturaId, 68m)], restantes.Select(m => (m.Id, m.ImporteRestante)));
        var saldo = JsonDocument.Parse(await Admin().GetStringAsync($"/api/clientes/{socio}/saldo")).RootElement;
        Assert.Equal(68m, saldo.GetProperty("saldo").GetDecimal());
    }

    [Fact]
    public async Task Errores_400ConElCampo_Y404DelSocioDeLaRuta()
    {
        var client = Admin();
        var socio = await CrearSocioAsync(client, "Cliente cobros 400");

        // Cuerpo vacío: cada campo obligatorio con su error.
        var vacio = await client.PostAsJsonAsync("/api/cobros", new { });
        await AssertErrorAsync(vacio, HttpStatusCode.BadRequest, "Importe");
        var errores = JsonDocument.Parse(await vacio.Content.ReadAsStringAsync()).RootElement.GetProperty("errors");
        Assert.True(errores.TryGetProperty("FechaRegistro", out _));

        // Socio inexistente en el cuerpo: 400, no 404. Cuenta de caja sin posteo directo (1102 CxC): 400.
        await AssertErrorAsync(
            await client.PostAsJsonAsync("/api/cobros", new { socioNegocioId = Guid.NewGuid(), importe = 5m, fechaRegistro = D10 }),
            HttpStatusCode.BadRequest, "SocioNegocioId");
        await AssertErrorAsync(
            await client.PostAsJsonAsync("/api/cobros", new { socioNegocioId = socio, importe = 5m, fechaRegistro = D10, cuentaCajaId = CuentaContableIds.CxC }),
            HttpStatusCode.BadRequest, "CuentaCajaId");

        // Aplicación con movimientos inexistentes: 400 en ambos campos.
        var aplicacion = await client.PostAsJsonAsync("/api/cobros/aplicaciones", new { movimientoFacturaId = long.MaxValue, movimientoPagoId = long.MaxValue - 1, importe = 1m });
        await AssertErrorAsync(aplicacion, HttpStatusCode.BadRequest, "MovimientoFacturaId");

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/clientes/{Guid.NewGuid()}/movimientos-abiertos")).StatusCode);
        Assert.Equal("[]", await client.GetStringAsync($"/api/clientes/{socio}/movimientos-abiertos"));
    }

    [Fact]
    public async Task Aplicacion_EntreSociosDistintos_400SociosDistintos_AunqueUnoEsteBloqueadoTodo_SinEscribir()
    {
        var client = Admin();
        var conFactura = await CrearSocioAsync(client, "Cliente con factura");
        var conPago = await CrearSocioAsync(client, "Cliente con pago");
        await PostearFacturaAsync(client, conFactura, 100m);
        var pago = await client.PostAsJsonAsync("/api/cobros", new { socioNegocioId = conPago, importe = 50m, fechaRegistro = D10 });
        Assert.True(pago.StatusCode == HttpStatusCode.OK, await pago.Content.ReadAsStringAsync());
        var pagoId = JsonDocument.Parse(await pago.Content.ReadAsStringAsync()).RootElement.GetProperty("movimientoClienteId").GetInt64();
        var facturaId = (await client.GetFromJsonAsync<List<MovimientoClienteResponse>>($"/api/clientes/{conFactura}/movimientos-abiertos"))![0].Id;

        // El socio del pago bloqueado para todo: el error sigue siendo socios_distintos (se comprueba antes de bloquear socios).
        await using var conexion = new NpgsqlConnection(_fixture.AppConnectionString);
        await conexion.ExecuteAsync("""UPDATE "SociosNegocio" SET "Bloqueado" = 2 WHERE "Id" = @Id""", new { Id = conPago });
        const string sqlFoto = """
            SELECT (SELECT COUNT(*) FROM "MovimientosClienteDetalle")::text || '/' ||
                   (SELECT COALESCE(pg_sequence_last_value(pg_get_serial_sequence('"MovimientosClienteDetalle"', 'Id')::regclass), 0))::text
            """;
        var antes = await conexion.ExecuteScalarAsync<string>(sqlFoto);

        await AssertErrorAsync(
            await client.PostAsJsonAsync("/api/cobros/aplicaciones", new { movimientoFacturaId = facturaId, movimientoPagoId = pagoId, importe = 10m }),
            HttpStatusCode.BadRequest, "MovimientoPagoId", "clientes distintos");

        Assert.Equal(antes, await conexion.ExecuteScalarAsync<string>(sqlFoto));
        var abiertosFactura = (await client.GetFromJsonAsync<List<MovimientoClienteResponse>>($"/api/clientes/{conFactura}/movimientos-abiertos"))!;
        var abiertosPago = (await client.GetFromJsonAsync<List<MovimientoClienteResponse>>($"/api/clientes/{conPago}/movimientos-abiertos"))!;
        Assert.Equal([118m], abiertosFactura.Select(m => m.ImporteRestante));
        Assert.Equal([-50m], abiertosPago.Select(m => m.ImporteRestante));
    }

    // ----- Helpers -----

    private static async Task PostearFacturaAsync(HttpClient client, Guid socio, decimal baseImponible)
    {
        var cuenta = await CrearCuentaAsync(client);
        var creado = await client.PostAsJsonAsync("/api/facturas-venta/borradores", new { socioNegocioId = socio, fechaRegistro = D10 });
        Assert.True(creado.StatusCode == HttpStatusCode.Created, await creado.Content.ReadAsStringAsync());
        var borrador = (await creado.Content.ReadFromJsonAsync<FacturaVentaBorradorResponse>())!;
        var linea = await client.PostAsJsonAsync($"/api/facturas-venta/borradores/{borrador.Id}/lineas", new
        {
            tipo = TipoLineaFactura.CuentaContable, cuentaContableId = cuenta, cantidad = 1m, precioUnitario = baseImponible,
            grupoIvaProductoId = GrupoContableIds.IvaProductoItbis18
        });
        Assert.True(linea.StatusCode == HttpStatusCode.Created, await linea.Content.ReadAsStringAsync());
        var posteo = await client.PostAsync($"/api/facturas-venta/borradores/{borrador.Id}/postear", null);
        Assert.True(posteo.StatusCode == HttpStatusCode.OK, await posteo.Content.ReadAsStringAsync());
    }

    private static async Task<Guid> CrearSocioAsync(HttpClient client, string nombre)
    {
        var response = await client.PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = nombre });
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CrearCuentaAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/cuentas-contables", new
        {
            numero = $"47{Random.Shared.Next(10_000, 99_999)}", nombre = "Otros ingresos", tipoCuenta = TipoCuentaContable.Posteo,
            tipoResultado = TipoResultadoCuenta.Resultado, posteoDirecto = true, bloqueada = false, sangria = 1
        });
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

    private HttpClient Admin() => Rol("Administrador");

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
