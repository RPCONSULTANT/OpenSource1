extern alias BlazorApp;

using System.Net;
using System.Text;
using BlazorApp::OpenSource1.Blazor.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace OpenSource1.SmokeTests.Blazor;

public sealed class BusquedaApiClientTests
{
    [Fact]
    public async Task Buscar_EscapaQ_EnviaLimite_YDeserializa()
    {
        const string json = """
            {"grupos":[{"tipo":"clientes","titulo":"Clientes","items":[{"tipo":"clientes","id":"1","titulo":"Comercial A&B","subtitulo":null,"ruta":"/clientes/1"}]}]}
            """;
        var handler = new Grabador(HttpStatusCode.OK, json);
        var cliente = new BusquedaApiClient(Http(handler), NullLogger<BusquedaApiClient>.Instance);

        var resultado = await cliente.BuscarAsync("  a&b c ", 7);

        Assert.True(resultado.Succeeded);
        Assert.Equal("/api/busqueda", handler.Uri!.AbsolutePath);
        Assert.Equal("?q=a%26b%20c&limite=7", handler.Uri.Query);
        Assert.Equal("Comercial A&B", resultado.Valor!.Grupos[0].Items[0].Titulo);
    }

    [Fact]
    public async Task Buscar_400_DevuelveLosMensajesReales()
    {
        const string cuerpo = """{"title":"x","status":400,"errors":{"q":["El texto a buscar debe tener entre 2 y 100 caracteres."]}}""";
        var cliente = new BusquedaApiClient(Http(new Grabador(HttpStatusCode.BadRequest, cuerpo)), NullLogger<BusquedaApiClient>.Instance);

        var resultado = await cliente.BuscarAsync("ab");

        Assert.False(resultado.Succeeded);
        Assert.Contains("El texto a buscar debe tener entre 2 y 100 caracteres.", resultado.Errors!);
    }

    [Fact]
    public async Task Buscar_403_MensajeDePermisos()
    {
        var cliente = new BusquedaApiClient(Http(new Grabador(HttpStatusCode.Forbidden, "")), NullLogger<BusquedaApiClient>.Instance);

        var resultado = await cliente.BuscarAsync("ab");

        Assert.Equal("No tiene permisos para buscar registros.", resultado.Message);
    }

    private static HttpClient Http(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("http://api.test/") };

    private sealed class Grabador(HttpStatusCode estado, string cuerpo) : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(estado) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") });
        }
    }
}
