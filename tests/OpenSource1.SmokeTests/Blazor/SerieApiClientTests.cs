extern alias BlazorApp;
using System.Net;
using System.Text;
using System.Text.Json;
using BlazorApp::OpenSource1.Blazor.Services;
using Microsoft.Extensions.Logging.Abstractions;
using OpenSource1.Core.Enums;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Clientes tipados de series, configuración y copia (spec no-series): rutas, cuerpos y traducción de errores.</summary>
public sealed class SerieApiClientTests
{
    [Fact]
    public async Task ActivasDelTipo_PideTipoYActivaOrdenadasPorCodigo()
    {
        var handler = new Grabador(HttpStatusCode.OK, """{"items":[],"pagina":1,"tamanoPagina":200,"total":0}""");
        var client = new SerieApiClient(Http(handler), NullLogger<SerieApiClient>.Instance);

        await client.ActivasDelTipoAsync(TipoDocumentoSerie.FacturaVenta);

        Assert.Equal("/api/series", handler.Uri!.AbsolutePath);
        Assert.Contains("tipo=2", handler.Uri.Query);
        Assert.Contains("activa=true", handler.Uri.Query);
        Assert.Contains("ordenarPor=Codigo", handler.Uri.Query);
    }

    [Fact]
    public async Task CreateLinea_EnviaElCuerpo_Y400DevuelveLosMensajes()
    {
        const string error = """{"title":"x","status":400,"errors":{"NumeroInicial":["El rango se solapa con la línea 00000001–99999999 de la serie FV."]}}""";
        var handler = new Grabador(HttpStatusCode.BadRequest, error);
        var client = new SerieApiClient(Http(handler), NullLogger<SerieApiClient>.Instance);
        var serie = Guid.NewGuid();

        var resultado = await client.CreateLineaAsync(serie, new LineaSerieInput("00000001", "00000100", null, null, new DateOnly(2026, 1, 1), 1, false));

        Assert.False(resultado.Succeeded);
        Assert.Contains(resultado.Errors!, e => e.Contains("se solapa"));
        Assert.Equal($"/api/series/{serie}/lineas", handler.Uri!.AbsolutePath);
        using var cuerpo = JsonDocument.Parse(handler.Cuerpo!);
        Assert.Equal("00000100", cuerpo.RootElement.GetProperty("numeroFinal").GetString());
    }

    [Fact]
    public async Task UpdateLinea_PutConXminYBloqueada_SinUltimoUsado()
    {
        var handler = new Grabador(HttpStatusCode.OK, """{"id":"00000000-0000-0000-0000-000000000002","numeroInicial":"00000001","numeroFinal":"00000200","xmin":4}""");
        var client = new SerieApiClient(Http(handler), NullLogger<SerieApiClient>.Instance);
        var serie = Guid.NewGuid();
        var linea = Guid.NewGuid();

        var resultado = await client.UpdateLineaAsync(
            serie, linea, new LineaSerieInput("00000001", "00000200", "00000190", "00000009", new DateOnly(2026, 1, 1), 1, true), xmin: 3);

        Assert.True(resultado.Succeeded);
        Assert.Equal(HttpMethod.Put, handler.Metodo);
        Assert.Equal($"/api/series/{serie}/lineas/{linea}", handler.Uri!.AbsolutePath);
        using var cuerpo = JsonDocument.Parse(handler.Cuerpo!);
        Assert.Equal(3, cuerpo.RootElement.GetProperty("xmin").GetInt64());
        Assert.True(cuerpo.RootElement.GetProperty("bloqueada").GetBoolean());
        Assert.Equal("00000190", cuerpo.RootElement.GetProperty("numeroAviso").GetString());
        Assert.False(cuerpo.RootElement.TryGetProperty("ultimoNumeroUsado", out _));
    }

    [Fact]
    public async Task Proximo_404EsElMensajeDeNoEncontrada_Y200DevuelveNumeroYAviso()
    {
        var serie = Guid.NewGuid();
        var ok = new Grabador(HttpStatusCode.OK, """{"numero":"FV-000012","aviso":"Quedan 3."}""");

        var proximo = await new SerieApiClient(Http(ok), NullLogger<SerieApiClient>.Instance).ProximoAsync(serie);
        var noEncontrada = await new SerieApiClient(Http(new Grabador(HttpStatusCode.NotFound, "")), NullLogger<SerieApiClient>.Instance).ProximoAsync(serie);

        Assert.Equal($"/api/series/{serie}/proximo", ok.Uri!.AbsolutePath);
        Assert.Equal("FV-000012", proximo.Valor!.Numero);
        Assert.Equal("Quedan 3.", proximo.Valor.Aviso);
        Assert.False(noEncontrada.Succeeded);
        Assert.Contains("No se encontró la serie", noEncontrada.Message);
    }

    [Fact]
    public async Task Configuracion_PutPorTipoConXmin()
    {
        var handler = new Grabador(HttpStatusCode.OK, """{"tipoDocumento":5,"serieId":"00000000-0000-0000-0000-000000000001","serieCodigo":"X","serieDescripcion":"X","serieActiva":true,"xmin":2}""");
        var client = new ConfiguracionNumeracionApiClient(Http(handler), NullLogger<ConfiguracionNumeracionApiClient>.Instance);

        var resultado = await client.UpdateAsync(TipoDocumentoSerie.Cobro, Guid.Parse("00000000-0000-0000-0000-000000000001"), 9);

        Assert.True(resultado.Succeeded);
        Assert.Equal("/api/configuracion/numeracion/5", handler.Uri!.AbsolutePath);
        Assert.Equal(9, JsonDocument.Parse(handler.Cuerpo!).RootElement.GetProperty("xmin").GetInt64());
    }

    [Fact]
    public async Task Series_AltaModificacionBorradoYListadoPorCodigo()
    {
        var id = Guid.NewGuid();
        var input = new SerieInput("FV2", "Facturas 2", TipoDocumentoSerie.FacturaVenta, false, true);
        const string serieJson = """{"id":"00000000-0000-0000-0000-000000000005","codigo":"FV2","xmin":8}""";
        var alta = new Grabador(HttpStatusCode.Created, serieJson);
        var modificacion = new Grabador(HttpStatusCode.OK, serieJson);
        var borrado = new Grabador(HttpStatusCode.NoContent, "");
        var borradoLinea = new Grabador(HttpStatusCode.Conflict, """{"title":"La línea está usada."}""");
        var listado = new Grabador(HttpStatusCode.OK, """{"items":[],"pagina":1,"tamanoPagina":50,"total":0}""");

        var creada = await new SerieApiClient(Http(alta), NullLogger<SerieApiClient>.Instance).CreateAsync(input);
        await new SerieApiClient(Http(modificacion), NullLogger<SerieApiClient>.Instance).UpdateAsync(id, input, xmin: 7);
        var eliminada = await new SerieApiClient(Http(borrado), NullLogger<SerieApiClient>.Instance).DeleteAsync(id, xmin: 9);
        var lineaNo = await new SerieApiClient(Http(borradoLinea), NullLogger<SerieApiClient>.Instance).DeleteLineaAsync(id, Guid.Empty);
        await new SerieApiClient(Http(listado), NullLogger<SerieApiClient>.Instance).ListAsync(new SerieFiltro(" fv ", null, null));

        Assert.True(creada.Succeeded);
        Assert.Equal("FV2", creada.Valor!.Codigo);
        using (var cuerpo = JsonDocument.Parse(alta.Cuerpo!))
        {
            Assert.Equal(2, cuerpo.RootElement.GetProperty("tipoDocumento").GetInt32());
            Assert.True(cuerpo.RootElement.GetProperty("activa").GetBoolean());
        }

        Assert.Equal(HttpMethod.Put, modificacion.Metodo);
        Assert.Equal($"/api/series/{id}", modificacion.Uri!.AbsolutePath);
        using (var cuerpo = JsonDocument.Parse(modificacion.Cuerpo!))
        {
            Assert.Equal(7, cuerpo.RootElement.GetProperty("xmin").GetInt64());
            Assert.Equal("Facturas 2", cuerpo.RootElement.GetProperty("descripcion").GetString());
        }

        Assert.True(eliminada.Succeeded);
        Assert.Equal(HttpMethod.Delete, borrado.Metodo);
        Assert.Equal($"/api/series/{id}", borrado.Uri!.AbsolutePath);
        Assert.Equal("?xmin=9", borrado.Uri.Query);
        Assert.Equal("La línea está usada.", lineaNo.Message);
        Assert.Contains("codigo=fv", listado.Uri!.Query);
        Assert.Contains("ordenarPor=Codigo", listado.Uri.Query);
    }

    [Fact]
    public async Task Configuracion_ListaYPutConSerieId()
    {
        var serie = Guid.NewGuid();
        var lista = new Grabador(HttpStatusCode.OK, $$"""[{"tipoDocumento":2,"serieId":"{{serie}}","serieCodigo":"FV","serieDescripcion":"Facturas","serieActiva":true,"xmin":3}]""");
        var put = new Grabador(HttpStatusCode.OK, $$"""{"tipoDocumento":2,"serieId":"{{serie}}","xmin":4}""");

        var filas = await new ConfiguracionNumeracionApiClient(Http(lista), NullLogger<ConfiguracionNumeracionApiClient>.Instance).ListAsync();
        await new ConfiguracionNumeracionApiClient(Http(put), NullLogger<ConfiguracionNumeracionApiClient>.Instance).UpdateAsync(TipoDocumentoSerie.FacturaVenta, serie, 3);

        Assert.Equal("/api/configuracion/numeracion", lista.Uri!.AbsolutePath);
        Assert.Equal(TipoDocumentoSerie.FacturaVenta, Assert.Single(filas).TipoDocumento);
        Assert.Equal(serie, JsonDocument.Parse(put.Cuerpo!).RootElement.GetProperty("serieId").GetGuid());
    }

    [Fact]
    public async Task CopiarABorrador_PostALaRutaDeLaFactura_Y403EsElMensajeReal()
    {
        var handler = new Grabador(HttpStatusCode.Forbidden, "");
        var client = new FacturaVentaApiClient(Http(handler), NullLogger<FacturaVentaApiClient>.Instance);

        var resultado = await client.CopiarABorradorAsync("FV-000001");

        Assert.False(resultado.Succeeded);
        Assert.Equal("/api/facturas-venta/FV-000001/copiar-a-borrador", handler.Uri!.AbsolutePath);
        Assert.Contains("permiso", resultado.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CopiarABorrador_201DevuelveIdNumeroYAvisos()
    {
        var id = Guid.NewGuid();
        var handler = new Grabador(HttpStatusCode.Created, $$"""{"borradorId":"{{id}}","numero":"FV-BORR-000009","avisos":["Línea 20000 omitida."]}""");

        var resultado = await new FacturaVentaApiClient(Http(handler), NullLogger<FacturaVentaApiClient>.Instance).CopiarABorradorAsync("00000007");

        Assert.True(resultado.Succeeded);
        Assert.Equal(id, resultado.Valor!.BorradorId);
        Assert.Equal("FV-BORR-000009", resultado.Valor.Numero);
        Assert.Single(resultado.Valor.Avisos);
    }

    [Fact]
    public async Task FacturaBorrador_AltaEnviaAmbasSeries_YModificacionSoloLaDeRegistro()
    {
        var serieBorrador = Guid.NewGuid();
        var serieRegistro = Guid.NewGuid();
        var input = new BorradorCabeceraInput(Guid.NewGuid(), null, null, null, null, null, null, serieBorrador, serieRegistro);
        var alta = new Grabador(HttpStatusCode.Created, """{"id":"00000000-0000-0000-0000-000000000003","xmin":1}""");
        var modificacion = new Grabador(HttpStatusCode.OK, """{"id":"00000000-0000-0000-0000-000000000003","xmin":2}""");

        await new FacturaVentaApiClient(Http(alta), NullLogger<FacturaVentaApiClient>.Instance).CreateBorradorAsync(input);
        await new FacturaVentaApiClient(Http(modificacion), NullLogger<FacturaVentaApiClient>.Instance).UpdateBorradorAsync(Guid.NewGuid(), input, 1);

        using (var cuerpo = JsonDocument.Parse(alta.Cuerpo!))
        {
            Assert.Equal(serieBorrador, cuerpo.RootElement.GetProperty("serieBorradorId").GetGuid());
            Assert.Equal(serieRegistro, cuerpo.RootElement.GetProperty("serieRegistroId").GetGuid());
        }

        using (var cuerpo = JsonDocument.Parse(modificacion.Cuerpo!))
        {
            Assert.Equal(serieRegistro, cuerpo.RootElement.GetProperty("serieRegistroId").GetGuid());
            Assert.False(cuerpo.RootElement.TryGetProperty("serieBorradorId", out _));
        }
    }

    [Fact]
    public async Task NotaBorrador_AltaEnviaLasSeries_YElListadoFiltraPorEstado()
    {
        var serieBorrador = Guid.NewGuid();
        var serieRegistro = Guid.NewGuid();
        var alta = new Grabador(HttpStatusCode.Created, """{"id":"00000000-0000-0000-0000-000000000004","xmin":1}""");
        var listado = new Grabador(HttpStatusCode.OK, """{"items":[],"pagina":1,"tamanoPagina":50,"total":0}""");

        await new NotaCreditoVentaApiClient(Http(alta), NullLogger<NotaCreditoVentaApiClient>.Instance)
            .CreateBorradorAsync(new NotaCreditoBorradorInput("00000007", null, null, null, false, false, serieBorrador, serieRegistro));
        await new NotaCreditoVentaApiClient(Http(listado), NullLogger<NotaCreditoVentaApiClient>.Instance)
            .ListBorradoresAsync(new NotaCreditoVentaBorradorFiltro(null, null, null, null, Estado: 3));

        using var cuerpo = JsonDocument.Parse(alta.Cuerpo!);
        Assert.Equal(serieBorrador, cuerpo.RootElement.GetProperty("serieBorradorId").GetGuid());
        Assert.Equal(serieRegistro, cuerpo.RootElement.GetProperty("serieRegistroId").GetGuid());
        Assert.Contains("estado=3", listado.Uri!.Query);
    }

    private static HttpClient Http(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("http://api.test/") };

    private sealed class Grabador(HttpStatusCode estado, string cuerpo) : HttpMessageHandler
    {
        public HttpMethod? Metodo { get; private set; }
        public Uri? Uri { get; private set; }
        public string? Cuerpo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Metodo = request.Method;
            Uri = request.RequestUri;
            Cuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(estado) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };
        }
    }
}
