using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

[Collection(PostgresCollection.Name)]
public sealed class SociosNegocioApiTests : IClassFixture<PostgresTestFixture>
{
    private readonly PostgresTestFixture _fixture;
    private readonly HttpClient _client;

    public SociosNegocioApiTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateFactory().CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Crud_And_Authorization_Work()
    {
        var anon = new HttpRequestMessage(HttpMethod.Get, "/api/socios-negocio");
        anon.Headers.Add("X-Test-Anonymous", "true");
        var anonymousResponse = await _client.SendAsync(anon);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        var forbiddenClient = CreateClient("Supervisor");
        var forbidden = await forbiddenClient.PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = "A", email = "a@test.local" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var client = CreateClient("Administrador");
        var email = $"juan-{Guid.NewGuid():N}@test.local";
        var create = await client.PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = "Juan Perez", email, telefono = "809-000-0000", direccionLinea1 = "Calle 1" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var body = await create.Content.ReadAsStringAsync();
        var created = JsonDocument.Parse(body).RootElement.GetProperty("id").GetGuid();

        var list = await client.GetAsync("/api/socios-negocio");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var paged = await list.Content.ReadFromJsonAsync<PagedResult<SocioNegocioResponse>>();
        Assert.NotNull(paged);
        Assert.Contains(paged!.Items, c => c.Id == created);
        Assert.True(paged.Total >= 1);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/socios-negocio/{created}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/socios-negocio/{created}", new { nombreComercial = "Juan Perez", email, telefono = "809-111-1111", direccionLinea1 = "Calle 2" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/socios-negocio/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/socios-negocio/{Guid.NewGuid()}")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Supervisor").DeleteAsync($"/api/socios-negocio/{created}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Ejecutor").DeleteAsync($"/api/socios-negocio/{created}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await CreateClient("Administrador").DeleteAsync($"/api/socios-negocio/{created}")).StatusCode);

        // Ya borrado (soft delete): consultar, modificar o volver a borrar responde 404, no 200/204.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/socios-negocio/{created}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/socios-negocio/{created}", new { nombreComercial = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/socios-negocio/{created}")).StatusCode);
    }

    [Fact]
    public async Task Create_ConTodosLosCamposNuevos_LosDevuelveYLosPersiste()
    {
        var client = CreateClient("Administrador");
        var termino = await CrearTerminoPagoAsync(client);
        var documento = $"RNC{Guid.NewGuid():N}"[..15];

        var create = await client.PostAsJsonAsync("/api/socios-negocio", new
        {
            nombreComercial = "Distribuidora Norte",
            razonSocial = "Distribuidora Norte SRL",
            tipo = (int)TipoSocioNegocio.Ambos,
            tipoDocumentoFiscal = (int)TipoDocumentoFiscal.Rnc,
            numeroDocumentoFiscal = documento,
            email = (string?)null,
            ciudad = "Santiago",
            terminoPagoId = termino,
            limiteCredito = 25000.75m,
            bloqueado = (int)BloqueoSocioNegocio.Facturacion
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var creado = await create.Content.ReadFromJsonAsync<SocioNegocioResponse>();

        var leido = await (await client.GetAsync($"/api/socios-negocio/{creado!.Id}")).Content.ReadFromJsonAsync<SocioNegocioResponse>();
        Assert.Matches(@"^\d{6}$", leido!.Codigo);
        Assert.Equal("Distribuidora Norte", leido.NombreComercial);
        Assert.Equal("Distribuidora Norte SRL", leido.RazonSocial);
        Assert.Equal(TipoSocioNegocio.Ambos, leido.Tipo);
        Assert.Equal(TipoDocumentoFiscal.Rnc, leido.TipoDocumentoFiscal);
        Assert.Equal(documento, leido.NumeroDocumentoFiscal);
        Assert.Null(leido.Email);
        Assert.Equal("Santiago", leido.Ciudad);
        Assert.Equal(termino, leido.TerminoPagoId);
        Assert.Equal(25000.75m, leido.LimiteCredito);
        Assert.Equal(BloqueoSocioNegocio.Facturacion, leido.Bloqueado);
    }

    [Fact]
    public async Task Create_SinCamposNuevos_UsaLosDefaults()
    {
        var client = CreateClient("Administrador");

        var create = await client.PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = "Solo nombre" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var creado = await create.Content.ReadFromJsonAsync<SocioNegocioResponse>();

        Assert.Equal(TipoSocioNegocio.Cliente, creado!.Tipo);
        Assert.Equal(TipoDocumentoFiscal.SinDocumento, creado.TipoDocumentoFiscal);
        Assert.Equal(0m, creado.LimiteCredito);
        Assert.Equal(BloqueoSocioNegocio.Ninguno, creado.Bloqueado);
        Assert.Null(creado.Email);
        Assert.Null(creado.TerminoPagoId);
    }

    [Fact]
    public async Task Create_ConcurrentesDiezAltas_EmitenDiezCodigosDistintosYConsecutivosSinHuecos()
    {
        var client = CreateClient("Administrador");

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 10).Select(async i =>
        {
            var r = await client.PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = $"Concurrente {i}" });
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
            return (await r.Content.ReadFromJsonAsync<SocioNegocioResponse>())!.Codigo;
        }));

        var numeros = respuestas.Select(int.Parse).OrderBy(n => n).ToList();
        Assert.Equal(10, respuestas.Distinct().Count());
        Assert.All(respuestas, c => Assert.Matches(@"^\d{6}$", c));
        // Consecutivos: sin huecos dentro del rango emitido.
        Assert.Equal(Enumerable.Range(numeros[0], 10), numeros);

        await AssertContadorSinHuecosAsync();
    }

    [Fact]
    public async Task Create_QueFallaTrasReservarElNumero_NoConsumeNumeroYElSiguienteAltaLoRecibe()
    {
        var client = CreateClient("Administrador");

        var primero = await AltaAsync(client, "Antes del fallo");

        // TerminoPagoId inexistente: el handler ya reservó el número (FOR UPDATE + UPDATE del
        // contador) cuando lo detecta; el rollback debe devolverlo.
        var fallido = await client.PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = "Fallido", terminoPagoId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, fallido.StatusCode);
        var problema = await fallido.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("TerminoPagoId", problema!.Errors.Keys);

        // Documento fiscal duplicado: otro camino de fallo tras reservar.
        var doc = $"DUP{Guid.NewGuid():N}"[..15];
        var conDoc = await client.PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = "Con doc", tipoDocumentoFiscal = 1, numeroDocumentoFiscal = doc });
        Assert.Equal(HttpStatusCode.Created, conDoc.StatusCode);
        var duplicado = await client.PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = "Duplicado", tipoDocumentoFiscal = 1, numeroDocumentoFiscal = doc });
        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);

        var siguiente = await AltaAsync(client, "Despues del fallo");

        // primero = n, "Con doc" = n+1, y los DOS fallos no consumieron nada: el siguiente es n+2.
        Assert.Equal(int.Parse(primero) + 2, int.Parse(siguiente));

        await AssertContadorSinHuecosAsync();
    }

    [Fact]
    public async Task Create_ElCodigoEnviadoPorElClienteSeIgnora()
    {
        var client = CreateClient("Administrador");

        var create = await client.PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = "Quiere codigo", codigo = "HACK-1" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var creado = await create.Content.ReadFromJsonAsync<SocioNegocioResponse>();

        Assert.NotEqual("HACK-1", creado!.Codigo);
        Assert.Matches(@"^\d{6}$", creado.Codigo);
    }

    [Fact]
    public async Task Update_NoPuedeCambiarElCodigo()
    {
        var client = CreateClient("Administrador");
        var create = await client.PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = "Original" });
        var creado = (await create.Content.ReadFromJsonAsync<SocioNegocioResponse>())!;

        var update = await client.PutAsJsonAsync($"/api/socios-negocio/{creado.Id}", new { nombreComercial = "Modificado", codigo = "HACK-2" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var actualizado = (await update.Content.ReadFromJsonAsync<SocioNegocioResponse>())!;
        Assert.Equal(creado.Codigo, actualizado.Codigo);
        Assert.Equal("Modificado", actualizado.NombreComercial);

        var leido = (await (await client.GetAsync($"/api/socios-negocio/{creado.Id}")).Content.ReadFromJsonAsync<SocioNegocioResponse>())!;
        Assert.Equal(creado.Codigo, leido.Codigo);
    }

    [Fact]
    public async Task Create_DocumentoFiscalDuplicado_Devuelve409ConElCampo_YSeReutilizaTrasBorrar()
    {
        var client = CreateClient("Administrador");
        var doc = $"CED{Guid.NewGuid():N}"[..15];
        var payload = new { nombreComercial = "Primero", tipoDocumentoFiscal = (int)TipoDocumentoFiscal.Cedula, numeroDocumentoFiscal = doc };

        var primero = await client.PostAsJsonAsync("/api/socios-negocio", payload);
        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);
        var id = (await primero.Content.ReadFromJsonAsync<SocioNegocioResponse>())!.Id;

        var duplicado = await client.PostAsJsonAsync("/api/socios-negocio", payload);
        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);
        var problema = await duplicado.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("NumeroDocumentoFiscal", problema!.Errors.Keys);

        // Borrado lógico: el índice único es parcial, el documento vuelve a estar disponible.
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/socios-negocio/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/socios-negocio", payload)).StatusCode);
    }

    [Fact]
    public async Task Update_DocumentoFiscalDeOtroSocio_Devuelve409_PeroElPropioSePermite()
    {
        var client = CreateClient("Administrador");
        var docA = $"PAS{Guid.NewGuid():N}"[..15];
        var docB = $"PAS{Guid.NewGuid():N}"[..15];

        var a = (await (await client.PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = "A", tipoDocumentoFiscal = 3, numeroDocumentoFiscal = docA })).Content.ReadFromJsonAsync<SocioNegocioResponse>())!;
        var b = (await (await client.PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = "B", tipoDocumentoFiscal = 3, numeroDocumentoFiscal = docB })).Content.ReadFromJsonAsync<SocioNegocioResponse>())!;

        var choque = await client.PutAsJsonAsync($"/api/socios-negocio/{b.Id}", new { nombreComercial = "B", tipoDocumentoFiscal = 3, numeroDocumentoFiscal = docA });
        Assert.Equal(HttpStatusCode.Conflict, choque.StatusCode);

        var propio = await client.PutAsJsonAsync($"/api/socios-negocio/{a.Id}", new { nombreComercial = "A2", tipoDocumentoFiscal = 3, numeroDocumentoFiscal = docA });
        Assert.Equal(HttpStatusCode.OK, propio.StatusCode);
    }

    [Fact]
    public async Task Create_TerminoPagoInexistenteOBorrado_Devuelve400ConElCampo()
    {
        var client = CreateClient("Administrador");

        var inexistente = await client.PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = "X", terminoPagoId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, inexistente.StatusCode);
        var problema = await inexistente.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("TerminoPagoId", problema!.Errors.Keys);

        var borrado = await CrearTerminoPagoAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/terminos-pago/{borrado}")).StatusCode);
        var conBorrado = await client.PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = "X", terminoPagoId = borrado });
        Assert.Equal(HttpStatusCode.BadRequest, conBorrado.StatusCode);
    }

    [Fact]
    public async Task Create_LimiteCreditoNegativo_Devuelve400ConElCampo()
    {
        var client = CreateClient("Administrador");

        var response = await client.PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = "X", limiteCredito = -1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problema = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("LimiteCredito", problema!.Errors.Keys);
    }

    [Theory]
    [InlineData("""{"nombreComercial":""}""", "NombreComercial")]
    [InlineData("""{"nombreComercial":"X","tipo":99}""", "Tipo")]
    [InlineData("""{"nombreComercial":"X","tipoDocumentoFiscal":1}""", "NumeroDocumentoFiscal")]
    [InlineData("""{"nombreComercial":"X","email":"no-es-correo"}""", "Email")]
    [InlineData("""{"nombreComercial":"X","paisCodigo":"ZZ"}""", "PaisCodigo")]
    public async Task Create_DatosInvalidos_Devuelve400ConElCampoDelDto(string json, string campo)
    {
        var client = CreateClient("Administrador");

        var response = await client.PostAsync("/api/socios-negocio", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problema = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(campo, problema!.Errors.Keys);
    }

    [Fact]
    public async Task List_FiltraPorNombreComercial()
    {
        var client = CreateClient("Administrador");
        var marca = $"Zeta{Guid.NewGuid():N}"[..12];
        await AltaAsync(client, $"{marca} Uno");
        await AltaAsync(client, "Otro distinto");

        var response = await client.GetAsync($"/api/socios-negocio?nombreComercial={marca}*");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await response.Content.ReadFromJsonAsync<PagedResult<SocioNegocioResponse>>();

        Assert.Single(paged!.Items);
        Assert.StartsWith(marca, paged.Items[0].NombreComercial);
    }

    [Fact]
    public async Task List_RespetaElTamanoDePaginaYDevuelveElTotal()
    {
        var client = CreateClient("Administrador");

        for (var i = 0; i < 3; i++)
        {
            var create = await client.PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = $"Pag Cliente{i}", email = $"pag-{Guid.NewGuid():N}@test.local" });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        }

        var response = await client.GetAsync("/api/socios-negocio?tamanoPagina=2&pagina=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var paged = await response.Content.ReadFromJsonAsync<PagedResult<SocioNegocioResponse>>();
        Assert.NotNull(paged);
        Assert.Equal(2, paged!.Items.Count);
        Assert.True(paged.Total >= 3);
        Assert.True(paged.TotalPaginas >= 2);
        Assert.Equal(1, paged.Pagina);
        Assert.Equal(2, paged.TamanoPagina);
    }

    [Fact]
    public async Task List_ColumnaDeOrdenNoPermitida_CaeAlOrdenPorDefectoSinRomper()
    {
        var client = CreateClient("Administrador");

        var response = await client.GetAsync("/api/socios-negocio?ordenarPor=" + Uri.EscapeDataString("\"; DROP TABLE \"SociosNegocio") + "&tamanoPagina=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await response.Content.ReadFromJsonAsync<PagedResult<SocioNegocioResponse>>();
        Assert.NotNull(paged);
    }

    private static async Task<string> AltaAsync(HttpClient client, string nombreComercial)
    {
        var response = await client.PostAsJsonAsync("/api/socios-negocio", new { nombreComercial });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SocioNegocioResponse>())!.Codigo;
    }

    private static async Task<Guid> CrearTerminoPagoAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/terminos-pago", new
        {
            codigo = $"T{Guid.NewGuid():N}"[..10],
            descripcion = "Termino de prueba",
            diasVencimiento = 30,
            diasDescuento = 0,
            porcentajeDescuento = 0
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Invariante de "sin huecos": en esta base recién creada cada número consumido de la serie
    /// SOCIOS corresponde a una fila persistida (incluidas las borradas lógicamente), así que
    /// UltimoNumeroUsado debe ser igual al total de filas. Un alta fallida que consumiera número
    /// dejaría el contador por encima.
    /// </summary>
    private async Task AssertContadorSinHuecosAsync()
    {
        await using var conexion = new NpgsqlConnection(_fixture.AppConnectionString);
        await conexion.OpenAsync();

        await using var comando = new NpgsqlCommand(
            """
            SELECT l."UltimoNumeroUsado", (SELECT COUNT(*) FROM "SociosNegocio")
            FROM "LineasSerie" l JOIN "Series" s ON s."Id" = l."SerieId"
            WHERE s."Codigo" = 'SOCIOS'
            """, conexion);
        await using var lector = await comando.ExecuteReaderAsync();
        Assert.True(await lector.ReadAsync());

        Assert.Equal(lector.GetInt64(1), long.Parse(lector.GetString(0)));
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
