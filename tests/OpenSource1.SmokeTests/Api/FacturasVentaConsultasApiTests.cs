using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using OpenSource1.Application.Features.FacturasVenta.Posteadas.Dtos;
using OpenSource1.Application.Features.MovimientosCliente.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;
using static OpenSource1.SmokeTests.TestInfrastructure.LibroClientesSemilla;

namespace OpenSource1.SmokeTests.Api;

/// <summary>
/// Consultas de la Task 6.3 contra Postgres real: facturas posteadas (<c>GET api/facturas-venta</c> paginado y
/// <c>GET api/facturas-venta/{numero}</c> con líneas y líneas de IVA) y libro de clientes (<c>GET api/clientes/{id}/movimientos</c>
/// con el importe restante derivado y <c>GET api/clientes/{id}/saldo</c> = Σ detalle). Sin motor de posteo todavía (Task 6.4): los
/// datos se siembran por SQL directo (<see cref="LibroClientesSemilla"/>). REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class FacturasVentaConsultasApiTests : IClassFixture<PostgresTestFixture>
{
    private const string Base = "/api/facturas-venta";
    private static readonly DateOnly D05 = new(2026, 9, 5);
    private static readonly DateOnly D10 = new(2026, 9, 10);
    private static readonly DateOnly D20 = new(2026, 9, 20);

    private readonly PostgresTestFixture _fixture;
    private readonly HttpClient _client;

    public FacturasVentaConsultasApiTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateFactory().CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    // ----- Facturas posteadas -----

    [Fact]
    public async Task Detalle_DevuelveCabeceraLineasEIva_ConCodigosResueltos_Y404SiNoExiste()
    {
        var client = Admin();
        var producto = await CrearProductoAsync(client);
        await using var conexion = await AbrirAsync();
        var socio = await InsertarSocioAsync(conexion, "Detalle");
        var registro = await InsertarRegistroContableAsync(conexion, "DETALLE");
        var numero = Numero();

        // Líneas insertadas fuera de orden: la respuesta las ordena por NumeroLinea; las de IVA, por identificador.
        await InsertarFacturaAsync(conexion, numero, socio, D10,
            [
                new Linea(30000, TipoLineaFactura.Comentario, 0m, Descripcion: "Gracias"),
                new Linea(10000, TipoLineaFactura.Producto, 30.09m, ProductoId: producto),
                new Linea(20000, TipoLineaFactura.CuentaContable, 50m, "EXENTO", 0m, CuentaContableId: CuentaContableIds.Ventas),
            ],
            [new LineaIva("ITBIS18", 18m, 30.09m, 5.42m), new LineaIva("EXENTO", 0m, 50m, 0m)],
            registro);

        var detalle = (await client.GetFromJsonAsync<FacturaVentaDetalleResponse>($"{Base}/{numero}"))!;

        var cabecera = detalle.Cabecera;
        Assert.Equal((numero, $"B{numero}", socio, socio), (cabecera.Numero, cabecera.NumeroBorrador, cabecera.SocioNegocioId, cabecera.SocioNegocioFacturarAId));
        Assert.Equal(("Detalle", "Cliente posteado", "Razón SRL"), (cabecera.SocioNegocioNombre, cabecera.NombreFacturacion, cabecera.RazonSocialFacturacion));
        Assert.Equal((D10, D10, D10.AddDays(30)), (cabecera.FechaRegistro, cabecera.FechaDocumento, cabecera.FechaVencimiento));
        Assert.Equal((80.09m, 5.42m, 85.51m), (cabecera.ImporteSinIva, cabecera.ImporteIva, cabecera.ImporteTotal));
        Assert.Equal(("PRINCIPAL", "DOP", TipoDocumentoFiscal.Rnc), (cabecera.AlmacenCodigo, cabecera.Moneda, cabecera.TipoDocumentoFiscal));
        Assert.Equal(registro, cabecera.RegistroContableId);
        Assert.False(string.IsNullOrEmpty(cabecera.NumeroRegistroContable));
        Assert.Equal(3, cabecera.NumeroLineas);

        Assert.Equal([10000, 20000, 30000], detalle.Lineas.Select(l => l.NumeroLinea));
        var lineaProducto = detalle.Lineas[0];
        Assert.Equal((TipoLineaFactura.Producto, producto, 30.09m, "ITBIS18", 18m),
            (lineaProducto.Tipo, lineaProducto.ProductoId, lineaProducto.ImporteLinea, lineaProducto.IdentificadorIva, lineaProducto.PorcentajeIva));
        Assert.False(string.IsNullOrEmpty(lineaProducto.ProductoCodigo));
        Assert.Equal("PRINCIPAL", lineaProducto.AlmacenCodigo);
        Assert.Equal((CuentaContableIds.Ventas, "4101"), (detalle.Lineas[1].CuentaContableId, detalle.Lineas[1].CuentaContableNumero));
        Assert.Equal((TipoLineaFactura.Comentario, "Gracias", (string?)null), (detalle.Lineas[2].Tipo, detalle.Lineas[2].Descripcion, detalle.Lineas[2].IdentificadorIva));

        Assert.Equal(
            [("EXENTO", 0m, 50m, 0m, "2101"), ("ITBIS18", 18m, 30.09m, 5.42m, "2101")],
            detalle.LineasIva.Select(l => (l.IdentificadorIva, l.PorcentajeIva, l.BaseImponible, l.ImporteIva, l.CuentaIvaNumero)));
        Assert.All(detalle.LineasIva, l => Assert.Equal(CuentaContableIds.IvaPorPagar, l.CuentaIvaId));

        // Totales = suma de grupos (Review Focus 1) también en el documento leído.
        Assert.Equal(cabecera.ImporteIva, detalle.LineasIva.Sum(l => l.ImporteIva));
        Assert.Equal(cabecera.ImporteSinIva, detalle.LineasIva.Sum(l => l.BaseImponible));

        await AssertErrorAsync(await client.GetAsync($"{Base}/NOEXISTE"), HttpStatusCode.NotFound, "Numero");
        Assert.Equal(HttpStatusCode.OK, (await Rol("Ejecutor").GetAsync($"{Base}/{numero}")).StatusCode);
    }

    [Fact]
    public async Task Listado_Paginado_Filtros_Orden_YRutasDeBorradoresIntactas()
    {
        var client = Admin();
        await using var conexion = await AbrirAsync();
        var socio = await InsertarSocioAsync(conexion, "Listado");
        var otro = await InsertarSocioAsync(conexion, "Otro");
        var prefijo = Numero()[..8];
        var (n1, n2, n3, n4) = ($"{prefijo}01", $"{prefijo}02", $"{prefijo}03", $"{prefijo}04");
        await InsertarFacturaAsync(conexion, n1, socio, D05, [], [new LineaIva("ITBIS18", 18m, 100m, 18m)], null, "Alfa");
        await InsertarFacturaAsync(conexion, n2, socio, D10, [], [new LineaIva("ITBIS18", 18m, 10m, 1.8m)], null, "Beta");
        await InsertarFacturaAsync(conexion, n3, socio, D20, [], [new LineaIva("EXENTO", 0m, 50m, 0m)], null, "Gamma");
        // Facturar-a distinto: cuenta para el socio vender-a y para el facturar-a.
        await InsertarFacturaAsync(conexion, n4, otro, D10, [], [new LineaIva("EXENTO", 0m, 1m, 0m)], null, "Delta", facturarAId: socio);

        // Por defecto: Numero descendente (lo más reciente primero), paginado.
        var p1 = await ListarAsync(client, $"socioId={socio}&tamanoPagina=3");
        Assert.Equal(4, p1.Total);
        Assert.Equal([n4, n3, n2], p1.Items.Select(f => f.Numero));
        var p2 = await ListarAsync(client, $"socioId={socio}&tamanoPagina=3&pagina=2");
        Assert.Equal([n1], p2.Items.Select(f => f.Numero));
        Assert.Equal(0, p1.Items[0].NumeroLineas);

        // Orden explícito por importe ascendente (desempate estable por Numero).
        var porImporte = await ListarAsync(client, $"socioId={socio}&ordenarPor=ImporteTotal&descendente=false");
        Assert.Equal([n4, n2, n3, n1], porImporte.Items.Select(f => f.Numero));
        Assert.Equal([1m, 11.8m, 50m, 118m], porImporte.Items.Select(f => f.ImporteTotal));

        // Filtros: rango de FechaRegistro (incluido), número, nombre de facturación, y un orden no permitido cae al defecto.
        Assert.Equal([n4, n2], (await ListarAsync(client, $"socioId={socio}&desde=2026-09-10&hasta=2026-09-10")).Items.Select(f => f.Numero));
        Assert.Equal([n3], (await ListarAsync(client, $"numero={n3}")).Items.Select(f => f.Numero));
        Assert.Equal([n2], (await ListarAsync(client, $"socioId={socio}&nombreFacturacion=bet")).Items.Select(f => f.Numero));
        Assert.Equal([n4], (await ListarAsync(client, $"socioId={otro}")).Items.Select(f => f.Numero));
        Assert.Equal(4, (await ListarAsync(client, $"socioId={socio}&ordenarPor=xmin;DROP")).Items.Count);

        await AssertErrorAsync(await client.GetAsync($"{Base}?desde=2026-09-20&hasta=2026-09-10"), HttpStatusCode.BadRequest, "Desde");

        // Roles: consultar = CanConsult; anónimo 401.
        Assert.Equal(HttpStatusCode.OK, (await Rol("Ejecutor").GetAsync($"{Base}?socioId={socio}")).StatusCode);
        var anon = new HttpRequestMessage(HttpMethod.Get, Base);
        anon.Headers.Add("X-Test-Anonymous", "true");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(anon)).StatusCode);

        // Las rutas de borradores siguen resolviéndose a su acción (literal antes que {numero}).
        var borradores = await Admin().GetAsync($"{Base}/borradores");
        Assert.Equal(HttpStatusCode.OK, borradores.StatusCode);
        Assert.True(JsonDocument.Parse(await borradores.Content.ReadAsStringAsync()).RootElement.TryGetProperty("items", out _));
    }

    // ----- Copiar a borrador (spec no-series) -----

    [Fact]
    public async Task CopiarABorrador_FacturaInexistente_404_SinPermisoDeAlta_403_YAnonimo_401()
    {
        var noExiste = await Admin().PostAsync($"{Base}/NO-EXISTE/copiar-a-borrador", null);
        Assert.Equal(HttpStatusCode.NotFound, noExiste.StatusCode);
        Assert.Contains("No existe la factura NO-EXISTE", await noExiste.Content.ReadAsStringAsync());

        // Supervisor no tiene CanAdd (copiar a borrador es un alta).
        Assert.Equal(HttpStatusCode.Forbidden, (await Rol("Supervisor").PostAsync($"{Base}/NO-EXISTE/copiar-a-borrador", null)).StatusCode);

        var anonimo = new HttpRequestMessage(HttpMethod.Post, $"{Base}/NO-EXISTE/copiar-a-borrador");
        anonimo.Headers.Add("X-Test-Anonymous", "true");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(anonimo)).StatusCode);
    }

    [Fact]
    public async Task CopiarABorrador_FacturaPosteada_201_ConLocationDelBorrador()
    {
        var client = Admin();
        await using var conexion = await AbrirAsync();
        var socio = await InsertarSocioAsync(conexion, "Copia API");
        var numero = Numero();
        await InsertarFacturaAsync(conexion, numero, socio, D10,
            [
                new Linea(10000, TipoLineaFactura.CuentaContable, 50m, CuentaContableId: CuentaContableIds.Ventas),
                new Linea(20000, TipoLineaFactura.Comentario, 0m, Descripcion: "Gracias"),
            ],
            [new LineaIva("ITBIS18", 18m, 50m, 9m)], null);

        var respuesta = await client.PostAsync($"{Base}/{numero}/copiar-a-borrador", null);

        Assert.True(respuesta.StatusCode == HttpStatusCode.Created, await respuesta.Content.ReadAsStringAsync());
        var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;
        var id = cuerpo.GetProperty("borradorId").GetGuid();
        Assert.Equal(0, cuerpo.GetProperty("avisos").GetArrayLength());
        Assert.EndsWith($"{Base}/borradores/{id}", respuesta.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(respuesta.Headers.Location)).StatusCode);
    }

    [Fact]
    public async Task CopiarABorrador_LineaQueYaNoValida_201_ConAvisoYSinLaLinea()
    {
        var client = Admin();
        var producto = await CrearProductoAsync(client);
        await using var conexion = await AbrirAsync();
        var socio = await InsertarSocioAsync(conexion, "Copia API con aviso");
        var numero = Numero();
        await InsertarFacturaAsync(conexion, numero, socio, D10,
            [
                new Linea(10000, TipoLineaFactura.CuentaContable, 50m, CuentaContableId: CuentaContableIds.Ventas),
                new Linea(20000, TipoLineaFactura.Producto, 10m, ProductoId: producto),
            ],
            [new LineaIva("ITBIS18", 18m, 60m, 10.8m)], null);
        await conexion.ExecuteAsync("""UPDATE "Productos" SET "Bloqueado" = 2 WHERE "Id" = @Id""", new { Id = producto });

        var respuesta = await client.PostAsync($"{Base}/{numero}/copiar-a-borrador", null);

        Assert.True(respuesta.StatusCode == HttpStatusCode.Created, await respuesta.Content.ReadAsStringAsync());
        var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;
        var aviso = Assert.Single(cuerpo.GetProperty("avisos").EnumerateArray()).GetString();
        Assert.Contains("Línea 20000", aviso);
        var lineas = JsonDocument.Parse(await client.GetStringAsync($"{Base}/borradores/{cuerpo.GetProperty("borradorId").GetGuid()}/lineas")).RootElement;
        var linea = Assert.Single(lineas.EnumerateArray());
        Assert.Equal(CuentaContableIds.Ventas, linea.GetProperty("cuentaContableId").GetGuid());
    }

    // ----- Libro de clientes -----

    [Fact]
    public async Task MovimientosYSaldo_DerivadosDelDetalle_TrasFacturaPagoParcialYAplicacion()
    {
        var client = Admin();
        await using var conexion = await AbrirAsync();
        var socio = await InsertarSocioAsync(conexion, "Saldo");
        var ajeno = await InsertarSocioAsync(conexion, "Ajeno");

        var factura1 = await InsertarMovimientoAsync(conexion, socio, TipoDocumentoCliente.Factura, "F1", D05, 118m);
        var factura2 = await InsertarMovimientoAsync(conexion, socio, TipoDocumentoCliente.Factura, "F2", D10, 20.5m);
        var pago = await InsertarMovimientoAsync(conexion, socio, TipoDocumentoCliente.Pago, "P1", D20, -50m, TipoOrigenMovimiento.Cobro);
        await AplicarAsync(conexion, factura1, pago, 50m, D20);
        await InsertarMovimientoAsync(conexion, ajeno, TipoDocumentoCliente.Factura, "FA", D10, 999m);

        // Saldo = Σ detalle de los movimientos del socio: 118 + 20.5 − 50 (pago) + (−50 + 50) (aplicación) = 88.5.
        var saldoSql = await conexion.ExecuteScalarAsync<decimal>(
            """
            SELECT COALESCE(SUM(d."Importe"), 0) FROM "MovimientosClienteDetalle" d
            JOIN "MovimientosCliente" m ON m."Id" = d."MovimientoClienteId" WHERE m."SocioNegocioId" = @Socio
            """, new { Socio = socio });
        Assert.Equal(88.5m, saldoSql);

        var saldo = (await client.GetFromJsonAsync<SaldoClienteResponse>($"/api/clientes/{socio}/saldo"))!;
        Assert.Equal((socio, saldoSql, 2), (saldo.SocioNegocioId, saldo.Saldo, saldo.MovimientosAbiertos));

        // Movimientos: cronológicos por defecto; restante = Σ de su detalle; abierta = restante ≠ 0. El pago queda aplicado del todo.
        var movimientos = await MovimientosAsync(client, socio, "");
        Assert.Equal(3, movimientos.Total);
        Assert.Equal(
            [
                (factura1, TipoDocumentoCliente.Factura, "F1", 118m, 68m, true),
                (factura2, TipoDocumentoCliente.Factura, "F2", 20.5m, 20.5m, true),
                (pago, TipoDocumentoCliente.Pago, "P1", -50m, 0m, false),
            ],
            movimientos.Items.Select(m => (m.Id, m.TipoDocumento, m.NumeroDocumento, m.ImporteOriginal, m.ImporteRestante, m.Abierta)));
        Assert.Equal(saldo.Saldo, movimientos.Items.Sum(m => m.ImporteRestante));
        var primero = movimientos.Items[0];
        Assert.Equal((D05, D05.AddDays(30), CuentaContableIds.CxC, "1201", TipoOrigenMovimiento.FacturaVenta),
            (primero.FechaRegistro, primero.FechaVencimiento, primero.CuentaCxCId, primero.NumeroCuentaCxC, primero.TipoOrigen));

        // Filtros: solo abiertos, rango de fechas, paginado y orden descendente explícito.
        Assert.Equal([factura1, factura2], (await MovimientosAsync(client, socio, "soloAbiertos=true")).Items.Select(m => m.Id));
        Assert.Equal([pago], (await MovimientosAsync(client, socio, "soloAbiertos=false")).Items.Select(m => m.Id));
        Assert.Equal([factura2], (await MovimientosAsync(client, socio, "desde=2026-09-06&hasta=2026-09-19")).Items.Select(m => m.Id));
        var pagina2 = await MovimientosAsync(client, socio, "tamanoPagina=2&pagina=2&descendente=true");
        Assert.Equal(3L, pagina2.Total);
        Assert.Equal([factura1], pagina2.Items.Select(m => m.Id));
        Assert.Equal([pago, factura2, factura1],
            (await MovimientosAsync(client, socio, "ordenarPor=ImporteOriginal&descendente=false")).Items.Select(m => m.Id));

        await AssertErrorAsync(await client.GetAsync($"/api/clientes/{socio}/movimientos?desde=2026-09-20&hasta=2026-09-10"),
            HttpStatusCode.BadRequest, "Desde");
    }

    [Fact]
    public async Task Clientes_SinMovimientos_SaldoCero_SocioInexistente404_YRoles()
    {
        var client = Admin();
        await using var conexion = await AbrirAsync();
        var socio = await InsertarSocioAsync(conexion, "Vacío");

        var saldo = (await client.GetFromJsonAsync<SaldoClienteResponse>($"/api/clientes/{socio}/saldo"))!;
        Assert.Equal((0m, 0), (saldo.Saldo, saldo.MovimientosAbiertos));
        Assert.Empty((await MovimientosAsync(client, socio, "")).Items);

        var inexistente = Guid.NewGuid();
        await AssertErrorAsync(await client.GetAsync($"/api/clientes/{inexistente}/saldo"), HttpStatusCode.NotFound, "Id");
        await AssertErrorAsync(await client.GetAsync($"/api/clientes/{inexistente}/movimientos"), HttpStatusCode.NotFound, "Id");

        // Un socio borrado lógicamente tampoco existe para estas consultas.
        await conexion.ExecuteAsync("""UPDATE "SociosNegocio" SET "IsDeleted" = true WHERE "Id" = @Id""", new { Id = socio });
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/clientes/{socio}/saldo")).StatusCode);

        var otro = await InsertarSocioAsync(conexion, "Roles");
        Assert.Equal(HttpStatusCode.OK, (await Rol("Ejecutor").GetAsync($"/api/clientes/{otro}/saldo")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Rol("Ejecutor").GetAsync($"/api/clientes/{otro}/movimientos")).StatusCode);
        var anon = new HttpRequestMessage(HttpMethod.Get, $"/api/clientes/{otro}/saldo");
        anon.Headers.Add("X-Test-Anonymous", "true");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(anon)).StatusCode);
    }

    // ----- Helpers -----

    private static string Numero() => Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

    private async Task<NpgsqlConnection> AbrirAsync()
    {
        var conexion = new NpgsqlConnection(_fixture.AppConnectionString);
        await conexion.OpenAsync();
        return conexion;
    }

    private static async Task<PagedResult<FacturaVentaResponse>> ListarAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"{Base}?{query}");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<PagedResult<FacturaVentaResponse>>())!;
    }

    private static async Task<PagedResult<MovimientoClienteResponse>> MovimientosAsync(HttpClient client, Guid socio, string query)
    {
        var response = await client.GetAsync($"/api/clientes/{socio}/movimientos?{query}");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<PagedResult<MovimientoClienteResponse>>())!;
    }

    private static async Task<Guid> CrearProductoAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/productos", new { codigo = $"PC{Guid.NewGuid():N}"[..11], nombre = "Producto posteado", precioVenta = 10.03m });
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string campo)
    {
        var cuerpo = await response.Content.ReadAsStringAsync();
        Assert.True(status == response.StatusCode, $"Se esperaba {status} y llegó {response.StatusCode}: {cuerpo}");
        Assert.True(
            JsonDocument.Parse(cuerpo).RootElement.GetProperty("errors").TryGetProperty(campo, out _),
            $"Se esperaba el campo '{campo}' en los errores: {cuerpo}");
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
