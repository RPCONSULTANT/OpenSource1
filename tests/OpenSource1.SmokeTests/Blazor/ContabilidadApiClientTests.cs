extern alias BlazorApp;
using System.Net;
using System.Text;
using BlazorApp::OpenSource1.Blazor.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary><see cref="ContabilidadApiClient"/> frente a respuestas HTTP simuladas (sin API real).</summary>
public sealed class ContabilidadApiClientTests
{
    [Fact]
    public async Task Postear_200ConCuerpoNulo_EsUnErrorConMensajeClaro()
    {
        var client = Cliente(HttpStatusCode.OK, "null");

        var resultado = await client.PostearCostoInventarioAsync();

        Assert.False(resultado.Succeeded);
        Assert.Null(resultado.Valor);
        Assert.Equal("La API no devolvió el resumen del batch de costo de inventario.", resultado.Message);
    }

    [Fact]
    public async Task Postear_200ConResumen_EsExito()
    {
        var client = Cliente(HttpStatusCode.OK, """{"asientos":2,"movimientosValorContabilizados":5,"pendientes":[]}""");

        var resultado = await client.PostearCostoInventarioAsync();

        Assert.True(resultado.Succeeded);
        Assert.NotNull(resultado.Valor);
        Assert.Equal(2, resultado.Valor.Asientos);
        Assert.Equal(5, resultado.Valor.MovimientosValorContabilizados);
    }

    private static ContabilidadApiClient Cliente(HttpStatusCode estado, string cuerpo) =>
        new(new HttpClient(new RespuestaFija(estado, cuerpo)) { BaseAddress = new Uri("http://api.test/") },
            NullLogger<ContabilidadApiClient>.Instance);

    private sealed class RespuestaFija(HttpStatusCode estado, string cuerpo) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(estado)
            {
                Content = new StringContent(cuerpo, Encoding.UTF8, "application/json"),
            });
    }
}
