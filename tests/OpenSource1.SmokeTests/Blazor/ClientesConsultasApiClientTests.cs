extern alias BlazorApp;
using System.Net;
using System.Text;
using BlazorApp::OpenSource1.Blazor.Services;
using Microsoft.Extensions.Logging.Abstractions;
using OpenSource1.Application.Features.MovimientosCliente;
using OpenSource1.Core.Common;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary><see cref="ClientesConsultasApiClient"/> (Task 7.3) frente a respuestas HTTP simuladas: URL de filtros y mensajes de la API.</summary>
public sealed class ClientesConsultasApiClientTests
{
    [Fact]
    public async Task Movimientos_ConstruyeLaUrlConTodosLosFiltrosYElOrden()
    {
        var handler = new RespuestaFija(HttpStatusCode.OK, """{"items":[],"pagina":1,"tamanoPagina":50,"total":0}""");
        var socio = Guid.NewGuid();

        var resultado = await Cliente(handler).ListMovimientosAsync(
            new MovimientoClienteSearchCriteria(socio, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), false, 3, new DateOnly(2026, 6, 30)),
            new PageRequest(2, 50, "FechaRegistro", Descendente: false));

        Assert.True(resultado.Succeeded);
        Assert.Equal(
            $"/api/clientes/{socio}/movimientos?desde=2026-06-01&hasta=2026-06-30&soloAbiertos=false&tipoDocumento=3&fechaCorte=2026-06-30"
            + "&pagina=2&tamanoPagina=50&ordenarPor=FechaRegistro&descendente=false",
            handler.Ultima!.PathAndQuery);
    }

    [Fact]
    public async Task EstadoCuenta_UrlYRespuesta_SoloConSaldoViajaSoloSiEsTrue()
    {
        var handler = new RespuestaFija(
            HttpStatusCode.OK,
            """
            {"pagina":{"items":[{"socioNegocioId":"6b5d5fed-2c8f-4c49-bbc2-b347583676f5","codigo":"C0001","nombre":"Alfa","corriente":59,
            "dias1a30":11.8,"dias31a60":136,"dias61a90":23.6,"mas90":118,"sinAplicar":-80,"total":268.4,"documentosAbiertos":6}],
            "pagina":1,"tamanoPagina":50,"total":1},"fechaCorte":"2026-06-30",
            "totales":{"corriente":59,"dias1a30":11.8,"dias31a60":136,"dias61a90":23.6,"mas90":118,"sinAplicar":-80,"total":268.4}}
            """);
        var socio = Guid.NewGuid();

        var resultado = await Cliente(handler).GetEstadoCuentaAsync(
            new EstadoCuentaCriterios(new DateOnly(2026, 6, 30), socio, " alfa beta ", SoloConSaldo: true),
            new PageRequest(1, 50, "Total", Descendente: true));

        Assert.Equal(
            $"/api/clientes/estado-cuenta?fechaCorte=2026-06-30&socioId={socio}&texto=alfa%20beta&soloConSaldo=true"
            + "&pagina=1&tamanoPagina=50&ordenarPor=Total&descendente=true",
            handler.Ultima!.PathAndQuery);
        Assert.True(resultado.Succeeded);
        var fila = Assert.Single(resultado.Valor!.Pagina.Items);
        Assert.Equal((268.4m, -80m, 6), (fila.Total, fila.SinAplicar, fila.DocumentosAbiertos));
        Assert.Equal((new DateOnly(2026, 6, 30), 268.4m), (resultado.Valor.FechaCorte, resultado.Valor.Totales.Total));

        await Cliente(handler).GetEstadoCuentaAsync(new EstadoCuentaCriterios(), new PageRequest(1, 50, null, false));
        Assert.Equal("/api/clientes/estado-cuenta?pagina=1&tamanoPagina=50&descendente=false", handler.Ultima!.PathAndQuery);
    }

    [Fact]
    public async Task Error400_MensajesReales_404ClienteNoEncontrado_500Generico()
    {
        var malo = await Cliente(new RespuestaFija(
                HttpStatusCode.BadRequest,
                """{"title":"Los datos enviados no son válidos.","status":400,"errors":{"TipoDocumento":["El tipo de documento 9 no es válido (1 Factura, 2 Nota de crédito, 3 Pago, 4 Ajuste)."]}}"""))
            .ListMovimientosAsync(new MovimientoClienteSearchCriteria(Guid.NewGuid(), TipoDocumento: 9), new PageRequest());
        Assert.False(malo.Succeeded);
        Assert.Equal("Revise los filtros:", malo.Message);
        Assert.Equal(["El tipo de documento 9 no es válido (1 Factura, 2 Nota de crédito, 3 Pago, 4 Ajuste)."], malo.Errors);

        var noExiste = await Cliente(new RespuestaFija(HttpStatusCode.NotFound, "{}"))
            .ListMovimientosAsync(new MovimientoClienteSearchCriteria(Guid.NewGuid()), new PageRequest());
        Assert.Equal((false, "No se encontró el cliente indicado."), (noExiste.Succeeded, noExiste.Message));

        var error = await Cliente(new RespuestaFija(HttpStatusCode.InternalServerError, "boom"))
            .GetEstadoCuentaAsync(new EstadoCuentaCriterios(), new PageRequest());
        Assert.Equal((false, "No fue posible cargar el estado de cuenta."), (error.Succeeded, error.Message));

        var nulo = await Cliente(new RespuestaFija(HttpStatusCode.OK, "null"))
            .GetEstadoCuentaAsync(new EstadoCuentaCriterios(), new PageRequest());
        Assert.Equal((false, "La API no devolvió el estado de cuenta."), (nulo.Succeeded, nulo.Message));
    }

    private static ClientesConsultasApiClient Cliente(RespuestaFija handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://api.test/") }, NullLogger<ClientesConsultasApiClient>.Instance);

    private sealed class RespuestaFija(HttpStatusCode estado, string cuerpo) : HttpMessageHandler
    {
        public Uri? Ultima { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Ultima = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(estado)
            {
                Content = new StringContent(cuerpo, Encoding.UTF8, "application/json"),
            });
        }
    }
}
