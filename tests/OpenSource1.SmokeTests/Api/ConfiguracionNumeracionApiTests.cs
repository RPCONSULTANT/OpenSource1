using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using OpenSource1.Application.Features.ConfiguracionNumeracion.Dtos;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

/// <summary>
/// <c>api/configuracion/numeracion</c> (spec no-series) contra Postgres real: todos consultan, solo el Administrador cambia; la
/// serie debe existir, estar activa y ser del tipo; xmin; y un alta posterior (código de cliente) usa la serie nueva. REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ConfiguracionNumeracionApiTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    private const string Ruta = "/api/configuracion/numeracion";
    private readonly HttpClient _client = fixture.CreateFactory().CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Theory]
    [InlineData("Supervisor")]
    [InlineData("Ejecutor")]
    public async Task Consultan_OchoFilas_PeroNoModifican_403(string rol)
    {
        var client = Rol(rol);

        var filas = (await client.GetFromJsonAsync<List<ConfiguracionNumeracionResponse>>(Ruta))!;
        var cambio = await client.PutAsJsonAsync($"{Ruta}/7", new { serieId = SerieClienteIds.SerieId, xmin = 1 });

        Assert.Equal(TipoDocumentoSerieNombres.Todos, filas.Select(f => f.TipoDocumento));
        Assert.Contains(filas, f => f.TipoDocumento == TipoDocumentoSerie.Cliente && f.SerieCodigo == "SOCIOS");
        Assert.Equal(HttpStatusCode.Forbidden, cambio.StatusCode);
    }

    [Fact]
    public async Task Administrador_CambiaLaSerieDeClientes_YElSiguienteAltaLaUsa()
    {
        var admin = Rol("Administrador");
        var prefijo = $"CL{Guid.NewGuid():N}"[..6].ToUpperInvariant() + "-";
        var serieId = await CrearSerieConLineaAsync(TipoDocumentoSerie.Cliente, prefijo, activa: true);
        var original = Fila(await ListarAsync(admin), TipoDocumentoSerie.Cliente);
        try
        {
            var cambio = await admin.PutAsJsonAsync($"{Ruta}/7", new { serieId, xmin = original.Xmin });
            Assert.True(cambio.StatusCode == HttpStatusCode.OK, await cambio.Content.ReadAsStringAsync());
            Assert.Equal(serieId, (await cambio.Content.ReadFromJsonAsync<ConfiguracionNumeracionResponse>())!.SerieId);

            var socio = await admin.PostAsJsonAsync("/api/socios-negocio", new { nombreComercial = "Cliente serie nueva" });
            Assert.True(socio.StatusCode == HttpStatusCode.Created, await socio.Content.ReadAsStringAsync());
            Assert.Equal($"{prefijo}000001", JsonDocument.Parse(await socio.Content.ReadAsStringAsync()).RootElement.GetProperty("codigo").GetString());

            // Obsoleto: el xmin de la fila ya cambió.
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"{Ruta}/7", new { serieId = SerieClienteIds.SerieId, xmin = original.Xmin })).StatusCode);
        }
        finally
        {
            await using var conexion = new NpgsqlConnection(fixture.AppConnectionString);
            await conexion.ExecuteAsync("""UPDATE "ConfiguracionesNumeracion" SET "SerieId" = @S WHERE "TipoDocumento" = 7""", new { S = SerieClienteIds.SerieId });
        }
    }

    [Fact]
    public async Task Validaciones_SerieDeOtroTipo_Inactiva_Inexistente_YTipoInvalido()
    {
        var admin = Rol("Administrador");
        var fila = Fila(await ListarAsync(admin), TipoDocumentoSerie.Cobro);
        var inactiva = await CrearSerieConLineaAsync(TipoDocumentoSerie.Cobro, $"CI{Guid.NewGuid():N}"[..6] + "-", activa: false);

        await AssertCampoAsync(await admin.PutAsJsonAsync($"{Ruta}/5", new { serieId = SerieClienteIds.SerieId, xmin = fila.Xmin }), HttpStatusCode.BadRequest, "SerieId");
        await AssertCampoAsync(await admin.PutAsJsonAsync($"{Ruta}/5", new { serieId = inactiva, xmin = fila.Xmin }), HttpStatusCode.BadRequest, "SerieId");
        await AssertCampoAsync(await admin.PutAsJsonAsync($"{Ruta}/5", new { serieId = Guid.NewGuid(), xmin = fila.Xmin }), HttpStatusCode.BadRequest, "SerieId");
        await AssertCampoAsync(await admin.PutAsJsonAsync($"{Ruta}/99", new { serieId = SerieClienteIds.SerieId, xmin = fila.Xmin }), HttpStatusCode.BadRequest, "TipoDocumento");
    }

    /// <summary>
    /// NS5 entre los dos módulos: una serie inactiva no se asigna (400 SerieId) y, una vez asignada, no se desactiva (409). El PUT de
    /// la configuración bloquea la fila de la serie FOR UPDATE antes de comprobar Activa, igual que la desactivación antes de mirar
    /// si está asignada: en cualquier orden, nunca queda asignada una serie inactiva.
    /// </summary>
    [Fact]
    public async Task SerieInactiva_NoSeAsigna_YLaAsignada_NoSeDesactiva()
    {
        var admin = Rol("Administrador");
        var original = Fila(await ListarAsync(admin), TipoDocumentoSerie.Cobro);
        var serieId = await CrearSerieConLineaAsync(TipoDocumentoSerie.Cobro, $"CA{Guid.NewGuid():N}"[..6].ToUpperInvariant() + "-", activa: false);
        try
        {
            await AssertCampoAsync(await admin.PutAsJsonAsync($"{Ruta}/5", new { serieId, xmin = original.Xmin }), HttpStatusCode.BadRequest, "SerieId");
            Assert.Equal(original.SerieId, Fila(await ListarAsync(admin), TipoDocumentoSerie.Cobro).SerieId);

            await using (var conexion = new NpgsqlConnection(fixture.AppConnectionString))
            {
                await conexion.ExecuteAsync("""UPDATE "Series" SET "Activa" = true WHERE "Id" = @Id""", new { Id = serieId });
            }

            var cambio = await admin.PutAsJsonAsync($"{Ruta}/5", new { serieId, xmin = original.Xmin });
            Assert.True(cambio.StatusCode == HttpStatusCode.OK, await cambio.Content.ReadAsStringAsync());

            var serie = (await admin.GetFromJsonAsync<JsonElement>($"/api/series/{serieId}")).GetProperty("serie");
            Assert.True(serie.GetProperty("asignada").GetBoolean());
            var desactivar = await admin.PutAsJsonAsync($"/api/series/{serieId}", new
            {
                codigo = serie.GetProperty("codigo").GetString(),
                descripcion = serie.GetProperty("descripcion").GetString(),
                tipoDocumento = (int)TipoDocumentoSerie.Cobro,
                permiteHuecos = false,
                activa = false,
                xmin = serie.GetProperty("xmin").GetInt64(),
            });
            await AssertCampoAsync(desactivar, HttpStatusCode.Conflict, "Activa");
        }
        finally
        {
            await using var conexion = new NpgsqlConnection(fixture.AppConnectionString);
            await conexion.ExecuteAsync("""UPDATE "ConfiguracionesNumeracion" SET "SerieId" = @S WHERE "TipoDocumento" = 5""", new { S = original.SerieId });
        }
    }

    private async Task<Guid> CrearSerieConLineaAsync(TipoDocumentoSerie tipo, string prefijo, bool activa)
    {
        var id = Guid.NewGuid();
        await using var conexion = new NpgsqlConnection(fixture.AppConnectionString);
        await conexion.ExecuteAsync(
            """
            INSERT INTO "Series" ("Id", "Codigo", "Descripcion", "TipoDocumento", "PermiteHuecos", "Activa", "CreatedAtUtc", "CreatedBy", "IsDeleted")
            VALUES (@Id, @Codigo, 'Serie de prueba', @Tipo, false, @Activa, now(), 'test', false);
            INSERT INTO "LineasSerie" ("Id", "SerieId", "NumeroInicial", "NumeroFinal", "UltimoNumeroUsado", "FechaInicial", "Incremento",
                                       "Bloqueada", "CreatedAtUtc", "CreatedBy", "IsDeleted")
            VALUES (gen_random_uuid(), @Id, @Inicial, @Final, '', DATE '2020-01-01', 1, false, now(), 'test', false);
            """,
            new { Id = id, Codigo = $"C{Guid.NewGuid():N}"[..12].ToUpperInvariant(), Tipo = (short)tipo, Activa = activa,
                  Inicial = $"{prefijo}000001", Final = $"{prefijo}999999" });
        return id;
    }

    private static async Task<List<ConfiguracionNumeracionResponse>> ListarAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<ConfiguracionNumeracionResponse>>(Ruta))!;

    private static ConfiguracionNumeracionResponse Fila(IEnumerable<ConfiguracionNumeracionResponse> filas, TipoDocumentoSerie tipo) =>
        filas.Single(f => f.TipoDocumento == tipo);

    private static async Task AssertCampoAsync(HttpResponseMessage response, HttpStatusCode status, string campo)
    {
        var cuerpo = await response.Content.ReadAsStringAsync();
        Assert.True(status == response.StatusCode, $"Se esperaba {status} y llegó {response.StatusCode}: {cuerpo}");
        Assert.True(JsonDocument.Parse(cuerpo).RootElement.GetProperty("errors").TryGetProperty(campo, out _), cuerpo);
    }

    private HttpClient Rol(string role)
    {
        _client.DefaultRequestHeaders.Remove("X-Test-User");
        _client.DefaultRequestHeaders.Remove("X-Test-Roles");
        _client.DefaultRequestHeaders.Add("X-Test-User", role.ToLowerInvariant());
        _client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return _client;
    }
}
