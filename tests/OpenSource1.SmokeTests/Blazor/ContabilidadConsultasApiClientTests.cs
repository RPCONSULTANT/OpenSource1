extern alias BlazorApp;
using System.Net;
using System.Text;
using BlazorApp::OpenSource1.Blazor.Services;
using Microsoft.Extensions.Logging.Abstractions;
using OpenSource1.Application.Features.Contabilidad;
using OpenSource1.Core.Common;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary><see cref="ContabilidadConsultasApiClient"/> (Task 7.4) frente a respuestas HTTP simuladas: URL de filtros y mensajes de la API.</summary>
public sealed class ContabilidadConsultasApiClientTests
{
    [Fact]
    public async Task Movimientos_ConstruyeLaUrlConTodosLosFiltrosYElOrden()
    {
        var handler = new RespuestaFija(HttpStatusCode.OK, """{"items":[],"pagina":1,"tamanoPagina":50,"total":0}""");
        var cuenta = Guid.NewGuid();
        var socio = Guid.NewGuid();

        var resultado = await Cliente(handler).ListMovimientosAsync(
            new MovimientoContableSearchCriteria(cuenta, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), 2, " FV 01 ", socio, 42),
            new PageRequest(2, 50, "FechaRegistro", Descendente: false));

        Assert.True(resultado.Succeeded);
        Assert.Equal(
            $"/api/contabilidad/movimientos?cuentaId={cuenta}&desde=2026-06-01&hasta=2026-06-30&tipoDocumento=2&numeroDocumento=FV%2001"
            + $"&socioId={socio}&registroId=42&pagina=2&tamanoPagina=50&ordenarPor=FechaRegistro&descendente=false",
            handler.Ultima!.PathAndQuery);

        await Cliente(handler).ListMovimientosAsync(new MovimientoContableSearchCriteria(null, null, null), new PageRequest(1, 50, null, false));
        Assert.Equal("/api/contabilidad/movimientos?pagina=1&tamanoPagina=50&descendente=false", handler.Ultima!.PathAndQuery);
    }

    [Fact]
    public async Task Balance_UrlYRespuesta()
    {
        var handler = new RespuestaFija(
            HttpStatusCode.OK,
            """
            {"desde":"2024-02-01","hasta":"2024-12-31","filas":[
             {"cuentaContableId":"f1000000-0000-0000-0000-000000000001","numero":"1","nombre":"Activos","tipoCuenta":2,"tipoResultado":2,
              "sangria":0,"esEncabezado":true,"borrada":false,"saldoInicial":null,"debitos":null,"creditos":null,"saldoFinal":null,"movimientos":null},
             {"cuentaContableId":"f1000000-0000-0000-0000-000000000003","numero":"1201","nombre":"CxC","tipoCuenta":1,"tipoResultado":2,
              "sangria":1,"esEncabezado":false,"borrada":false,"saldoInicial":141.6,"debitos":118,"creditos":141.6,"saldoFinal":118,"movimientos":2}],
             "totales":{"saldoInicial":0,"debitos":118,"creditos":141.6,"saldoFinal":0}}
            """);

        var resultado = await Cliente(handler).GetBalanceComprobacionAsync(new BalanceComprobacionCriterios(new DateOnly(2024, 2, 1), new DateOnly(2024, 12, 31)));

        Assert.Equal("/api/contabilidad/balance-comprobacion?desde=2024-02-01&hasta=2024-12-31", handler.Ultima!.PathAndQuery);
        Assert.True(resultado.Succeeded);
        Assert.Equal(2, resultado.Valor!.Filas.Count);
        Assert.True(resultado.Valor.Filas[0].EsEncabezado);
        Assert.Null(resultado.Valor.Filas[0].SaldoFinal);
        Assert.Equal((141.6m, 118m, 2), (resultado.Valor.Filas[1].SaldoInicial!.Value, resultado.Valor.Filas[1].SaldoFinal!.Value, resultado.Valor.Filas[1].Movimientos!.Value));

        await Cliente(handler).GetBalanceComprobacionAsync(new BalanceComprobacionCriterios());
        Assert.Equal("/api/contabilidad/balance-comprobacion", handler.Ultima!.PathAndQuery);
    }

    [Fact]
    public async Task Error400_MensajesReales_403Y500Genericos_200SinCuerpo()
    {
        var malo = await Cliente(new RespuestaFija(
                HttpStatusCode.BadRequest,
                """{"title":"Los datos enviados no son válidos.","status":400,"errors":{"TipoDocumento":["El tipo de documento 9 no es válido (0 Ninguno, 1 Costo de inventario, 2 Factura de venta, 3 Cobro)."]}}"""))
            .ListMovimientosAsync(new MovimientoContableSearchCriteria(null, null, null, TipoDocumento: 9), new PageRequest());
        Assert.False(malo.Succeeded);
        Assert.Equal("Revise los filtros:", malo.Message);
        Assert.Equal(["El tipo de documento 9 no es válido (0 Ninguno, 1 Costo de inventario, 2 Factura de venta, 3 Cobro)."], malo.Errors);

        var prohibido = await Cliente(new RespuestaFija(HttpStatusCode.Forbidden, "{}"))
            .GetBalanceComprobacionAsync(new BalanceComprobacionCriterios());
        Assert.Equal((false, "No tiene permisos para consultar el libro contable."), (prohibido.Succeeded, prohibido.Message));

        var error = await Cliente(new RespuestaFija(HttpStatusCode.InternalServerError, "boom"))
            .GetBalanceComprobacionAsync(new BalanceComprobacionCriterios());
        Assert.Equal((false, "No fue posible cargar el balance de comprobación."), (error.Succeeded, error.Message));

        var nulo = await Cliente(new RespuestaFija(HttpStatusCode.OK, "null"))
            .ListMovimientosAsync(new MovimientoContableSearchCriteria(null, null, null), new PageRequest());
        Assert.Equal((false, "La API no devolvió los movimientos contables."), (nulo.Succeeded, nulo.Message));
    }

    private static ContabilidadConsultasApiClient Cliente(RespuestaFija handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://api.test/") }, NullLogger<ContabilidadConsultasApiClient>.Instance);

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
