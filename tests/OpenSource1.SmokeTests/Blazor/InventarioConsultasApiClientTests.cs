extern alias BlazorApp;
using System.Net;
using System.Text;
using BlazorApp::OpenSource1.Blazor.Services;
using Microsoft.Extensions.Logging.Abstractions;
using OpenSource1.Application.Features.Inventario.Consultas;
using OpenSource1.Core.Common;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary><see cref="InventarioConsultasApiClient"/> (Task 7.2) frente a respuestas HTTP simuladas: URL de filtros y mensajes de la API.</summary>
public sealed class InventarioConsultasApiClientTests
{
    [Fact]
    public async Task MovimientosProducto_ConstruyeLaUrlConTodosLosFiltrosYElOrden()
    {
        var handler = new RespuestaFija(HttpStatusCode.OK, """{"items":[],"pagina":2,"tamanoPagina":50,"total":0}""");
        var producto = Guid.NewGuid();
        var almacen = Guid.NewGuid();

        var resultado = await Cliente(handler).ListMovimientosProductoAsync(
            new MovimientoProductoVistaCriterios(producto, almacen, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), 2, 99, " FV 01 "),
            new PageRequest(2, 50, "FechaRegistro", Descendente: false));

        Assert.True(resultado.Succeeded);
        Assert.Equal(
            $"/api/inventario/movimientos-producto?productoId={producto}&almacenId={almacen}&desde=2026-07-01&hasta=2026-07-31"
            + "&tipoMovimiento=2&tipoOrigen=99&numeroDocumento=FV%2001&pagina=2&tamanoPagina=50&ordenarPor=FechaRegistro&descendente=false",
            handler.Ultima!.PathAndQuery);
    }

    [Fact]
    public async Task MovimientosValorYExistencias_SoloAjustesYSoloConExistencia_ViajanSoloSiSonTrue()
    {
        var handler = new RespuestaFija(HttpStatusCode.OK, """{"items":[],"pagina":1,"tamanoPagina":50,"total":0}""");
        await Cliente(handler).ListMovimientosValorAsync(new MovimientoValorVistaCriterios(SoloAjustes: true), new PageRequest(1, 50, null, false));
        Assert.Equal("/api/inventario/movimientos-valor?soloAjustes=true&pagina=1&tamanoPagina=50&descendente=false", handler.Ultima!.PathAndQuery);

        await Cliente(handler).ListMovimientosValorAsync(new MovimientoValorVistaCriterios(), new PageRequest(1, 50, null, false));
        Assert.DoesNotContain("soloAjustes", handler.Ultima!.PathAndQuery, StringComparison.Ordinal);

        var existencias = new RespuestaFija(
            HttpStatusCode.OK, """{"pagina":{"items":[],"pagina":1,"tamanoPagina":50,"total":0},"fecha":"2026-07-31","valorTotal":12.5}""");
        var resultado = await Cliente(existencias).ListExistenciasAsync(
            new ExistenciaVistaCriterios(null, null, "arroz blanco", new DateOnly(2026, 7, 31), SoloConExistencia: true),
            new PageRequest(1, 50, "ProductoCodigo", false));
        Assert.Equal(
            "/api/inventario/existencias?texto=arroz%20blanco&fecha=2026-07-31&soloConExistencia=true&pagina=1&tamanoPagina=50&ordenarPor=ProductoCodigo&descendente=false",
            existencias.Ultima!.PathAndQuery);
        Assert.True(resultado.Succeeded);
        Assert.Equal((new DateOnly(2026, 7, 31), 12.5m), (resultado.Valor!.Fecha, resultado.Valor.ValorTotal));
    }

    [Fact]
    public async Task Error400_DevuelveLosMensajesRealesDeLaApi()
    {
        var handler = new RespuestaFija(
            HttpStatusCode.BadRequest,
            """{"title":"Los datos enviados no son válidos.","status":400,"errors":{"Desde":["La fecha 'desde' no puede ser posterior a 'hasta'."]}}""");

        var resultado = await Cliente(handler).ListMovimientosProductoAsync(new MovimientoProductoVistaCriterios(), new PageRequest());

        Assert.False(resultado.Succeeded);
        Assert.Equal("Revise los filtros:", resultado.Message);
        Assert.Equal(["La fecha 'desde' no puede ser posterior a 'hasta'."], resultado.Errors);
    }

    [Fact]
    public async Task Error500_MensajeGenerico_Y200SinCuerpo_NoEsExito()
    {
        var error = await Cliente(new RespuestaFija(HttpStatusCode.InternalServerError, "boom"))
            .ListExistenciasAsync(new ExistenciaVistaCriterios(), new PageRequest());
        Assert.False(error.Succeeded);
        Assert.Equal("No fue posible cargar las existencias.", error.Message);
        Assert.Empty(error.Errors!);

        var nulo = await Cliente(new RespuestaFija(HttpStatusCode.OK, "null"))
            .ListMovimientosValorAsync(new MovimientoValorVistaCriterios(), new PageRequest());
        Assert.False(nulo.Succeeded);
        Assert.Equal("La API no devolvió los movimientos de valor.", nulo.Message);
    }

    private static InventarioConsultasApiClient Cliente(RespuestaFija handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://api.test/") }, NullLogger<InventarioConsultasApiClient>.Instance);

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
