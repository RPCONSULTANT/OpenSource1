extern alias BlazorApp;
using System.Net;
using BlazorApp::OpenSource1.Blazor.Services;
using Moq;
using OpenSource1.Application.Features.ConfiguracionNumeracion.Dtos;
using OpenSource1.Application.Features.Series.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Series de numeración y configuración (spec no-series, Parte 3): lista, alta, ficha con líneas y configuración por tipo.</summary>
public sealed class SeriesPaginasTests
{
    private static readonly Guid IdSerie = Guid.Parse("5e000000-0000-0000-0000-000000000001");
    private static readonly Guid IdLinea = Guid.Parse("5e000000-0000-0000-0000-000000000002");
    private static readonly Guid IdOtra = Guid.Parse("5e000000-0000-0000-0000-000000000003");

    [Fact]
    public async Task Lista_Administrador_TipoProximoYAviso_YNuevaSerie()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), $"/series?sel={IdSerie}");

        Assert.Contains("data-testid=\"page-toolbar\"", html);
        Assert.Contains("href=\"/series/nueva\"", html);
        Assert.Contains("Factura de venta", html);
        Assert.Contains("FV-000013", html);
        Assert.Contains("quedan 2", html);
        Assert.Contains($"href=\"/series/{IdSerie}\"", html);
    }

    [Fact]
    public async Task Lista_Supervisor_SinNuevaNiEliminar()
    {
        using var app = Configurar(new BlazorSsrFactory());

        var html = await HtmlSsr.HtmlAsync(app.Cliente("Supervisor"), $"/series?sel={IdSerie}");

        Assert.DoesNotContain("href=\"/series/nueva\"", html);
        Assert.DoesNotContain("data-testid=\"accion-eliminar\"", html);
    }

    [Fact]
    public async Task Nueva_CreaYAbreLaFicha_YPostForzadoSinPermiso_MuestraElMensajeDeLaApi()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var api = app.Simular<ISerieApiClient>();
        api.Setup(c => c.CreateAsync(It.Is<SerieInput>(i => i.Codigo == "FV2" && i.TipoDocumento == TipoDocumentoSerie.FacturaVenta), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<SerieResponse>(true, "ok", new SerieResponse { Id = IdOtra }));
        var campos = new Dictionary<string, string>
        {
            ["Input.Codigo"] = "FV2", ["Input.Descripcion"] = "Facturas sucursal", ["Input.TipoDocumento"] = "2", ["Input.Activa"] = "true",
        };

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), "/series/nueva", "add-serie", campos);

        Assert.Equal($"/series/{IdOtra}?ok=creada", FormulariosSsr.Destino(respuesta));

        api.Setup(c => c.CreateAsync(It.IsAny<SerieInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<SerieResponse>(false, "No tiene permisos para administrar series."));
        var supervisor = app.Cliente("Supervisor");
        var html = await HtmlSsr.HtmlAsync(supervisor, "/series/nueva");
        var forzado = await FormulariosSsr.EnviarAsync(supervisor, "/series/nueva", "add-serie", campos);

        Assert.Contains("No tiene permiso para realizar esta acción.", html);
        Assert.Contains("value=\"add-serie\"", html);
        Assert.Contains("No tiene permisos para administrar series.", HtmlSsr.Decodificar(await forzado.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Ficha_CabeceraYLineas_AgregarLinea_YLaUsadaNoSeElimina()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var api = app.Simular<ISerieApiClient>();
        api.Setup(c => c.CreateLineaAsync(IdSerie, It.Is<LineaSerieInput>(i => i.NumeroInicial == "FV-100001" && i.FechaInicial == new DateOnly(2027, 1, 1)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<LineaSerieResponse>(true, "ok", new LineaSerieResponse()));
        var url = $"/series/{IdSerie}";

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), url);
        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), url, "add-linea-serie", new Dictionary<string, string>
        {
            ["AddLinea.NumeroInicial"] = "FV-100001", ["AddLinea.NumeroFinal"] = "FV-199999", ["AddLinea.FechaInicialTexto"] = "2027-01-01",
            ["AddLinea.Incremento"] = "1",
        });

        Assert.Contains("name=\"_handler\" value=\"update-serie\"", html);
        Assert.Contains("name=\"_handler\" value=\"add-linea-serie\"", html);
        Assert.Contains("FV-000012", html);
        Assert.Contains("Usada", html);
        Assert.DoesNotContain($"deleteLineaId={IdLinea}", html);
        Assert.Contains($"editLineaId={IdLinea}", html);
        Assert.Equal($"/series/{IdSerie}?ok=linea_creada", FormulariosSsr.Destino(respuesta));
    }

    [Fact]
    public async Task Ficha_EliminarLineaForzado_MuestraElConflictoDeLaApi()
    {
        using var app = Configurar(new BlazorSsrFactory());
        app.Simular<ISerieApiClient>()
            .Setup(c => c.DeleteLineaAsync(IdSerie, IdLinea, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<bool>(false, "La línea ya emitió números: no se puede eliminar; bloquéela."));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), $"/series/{IdSerie}", "delete-linea-serie",
            new Dictionary<string, string> { ["DeleteLinea.Id"] = IdLinea.ToString() });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("La línea ya emitió números", HtmlSsr.Decodificar(await respuesta.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Configuracion_GuardaSoloLasFilasCambiadas()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var api = app.Simular<IConfiguracionNumeracionApiClient>();
        api.Setup(c => c.UpdateAsync(TipoDocumentoSerie.FacturaVenta, IdOtra, 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<ConfiguracionNumeracionResponse>(true, "ok"));

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), "/configuracion/numeracion");
        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), "/configuracion/numeracion", "config-numeracion", new Dictionary<string, string>
        {
            ["Input.Filas[0].Tipo"] = "2", ["Input.Filas[0].SerieId"] = IdOtra.ToString(), ["Input.Filas[0].Xmin"] = "4",
            ["Input.Filas[1].Tipo"] = "5", ["Input.Filas[1].SerieId"] = IdSerie.ToString(), ["Input.Filas[1].Xmin"] = "7",
        });

        Assert.Contains("Factura de venta", html);
        Assert.Contains("Cobro de cliente", html);
        Assert.Equal("/configuracion/numeracion?ok=guardada", FormulariosSsr.Destino(respuesta));
        api.Verify(c => c.UpdateAsync(TipoDocumentoSerie.FacturaVenta, IdOtra, 4, It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(c => c.UpdateAsync(TipoDocumentoSerie.Cobro, It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Ficha_Supervisor_SoloLectura_YPostForzadoDeCabecera_MuestraElMensajeDeLaApi()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var api = app.Simular<ISerieApiClient>();
        api.Setup(c => c.UpdateAsync(IdSerie, It.IsAny<SerieInput>(), 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<SerieResponse>(false, "No tiene permisos para realizar esta operación."));
        var supervisor = app.Cliente("Supervisor");
        var url = $"/series/{IdSerie}";

        var html = await HtmlSsr.HtmlAsync(supervisor, url);
        var forzado = await FormulariosSsr.EnviarAsync(supervisor, url, "update-serie", new Dictionary<string, string>
        {
            ["UpdateInput.Codigo"] = "FV", ["UpdateInput.Descripcion"] = "Facturas", ["UpdateInput.TipoDocumento"] = "2", ["UpdateInput.Xmin"] = "3",
        });

        Assert.Contains("data-testid=\"documento-cabecera\"", html);
        Assert.DoesNotContain("data-testid=\"cabecera-editable\"", html);
        Assert.DoesNotContain("data-testid=\"fila-nueva\"", html);
        Assert.DoesNotContain($"editLineaId={IdLinea}", html);
        foreach (var formulario in new[] { "update-serie", "add-linea-serie", "update-linea-serie", "delete-linea-serie" })
        {
            Assert.Contains($"name=\"_handler\" value=\"{formulario}\"", html);
        }

        Assert.Equal(HttpStatusCode.OK, forzado.StatusCode);
        Assert.Contains("No tiene permisos para realizar esta operación.", HtmlSsr.Decodificar(await forzado.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Ficha_ModificarLinea_EnviaElIdYElXmin_YMuestraLosErroresDeLaApi()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var api = app.Simular<ISerieApiClient>();
        api.Setup(c => c.UpdateLineaAsync(IdSerie, IdLinea, It.Is<LineaSerieInput>(i => i.NumeroFinal == "FV-000020" && i.Bloqueada), 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<LineaSerieResponse>(false, "Revise los datos:", Errors: ["El número de aviso debe tener el mismo prefijo que la línea."]));
        var url = $"/series/{IdSerie}?editLineaId={IdLinea}";

        var html = await HtmlSsr.HtmlAsync(app.Cliente(), url);
        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), url, "update-linea-serie", new Dictionary<string, string>
        {
            ["UpdateLinea.Id"] = IdLinea.ToString(), ["UpdateLinea.Xmin"] = "8", ["UpdateLinea.NumeroInicial"] = "FV-000001",
            ["UpdateLinea.NumeroFinal"] = "FV-000020", ["UpdateLinea.FechaInicialTexto"] = "2020-01-01", ["UpdateLinea.Incremento"] = "1",
            ["UpdateLinea.Bloqueada"] = "true",
        });
        var cuerpo = HtmlSsr.Decodificar(await respuesta.Content.ReadAsStringAsync());

        Assert.Contains("data-testid=\"fila-edicion\"", html);
        Assert.Contains("value=\"FV-000014\"", html);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("Revise los datos:", cuerpo);
        Assert.Contains("El número de aviso debe tener el mismo prefijo que la línea.", cuerpo);
    }

    [Fact]
    public async Task Configuracion_Supervisor_SoloLectura_YPostForzado_MuestraElMensajeDeLaApi()
    {
        using var app = Configurar(new BlazorSsrFactory());
        var api = app.Simular<IConfiguracionNumeracionApiClient>();
        api.Setup(c => c.UpdateAsync(TipoDocumentoSerie.FacturaVenta, IdOtra, 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<ConfiguracionNumeracionResponse>(false, "No tiene permisos para realizar esta operación."));
        var supervisor = app.Cliente("Supervisor");

        var html = await HtmlSsr.HtmlAsync(supervisor, "/configuracion/numeracion");
        var forzado = await FormulariosSsr.EnviarAsync(supervisor, "/configuracion/numeracion", "config-numeracion", new Dictionary<string, string>
        {
            ["Input.Filas[0].Tipo"] = "2", ["Input.Filas[0].SerieId"] = IdOtra.ToString(), ["Input.Filas[0].Xmin"] = "4",
        });

        Assert.DoesNotContain("<select", html);
        Assert.Contains("value=\"config-numeracion\"", html);
        Assert.Contains("No tiene permisos para realizar esta operación.", HtmlSsr.Decodificar(await forzado.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Lista_EliminarForzado_MuestraElConflictoDeLaApi()
    {
        using var app = Configurar(new BlazorSsrFactory());
        app.Simular<ISerieApiClient>()
            .Setup(c => c.DeleteAsync(IdSerie, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VentaOperationResult<bool>(false, "La serie está asignada en la configuración de numeración: no se puede eliminar."));

        var respuesta = await FormulariosSsr.EnviarAsync(app.Cliente(), "/series", "delete-serie",
            new Dictionary<string, string> { ["DeleteInput.Id"] = IdSerie.ToString() });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("no se puede eliminar", HtmlSsr.Decodificar(await respuesta.Content.ReadAsStringAsync()));
    }

    private static BlazorSsrFactory Configurar(BlazorSsrFactory app)
    {
        var serie = new SerieResponse
        {
            Id = IdSerie, Codigo = "FV", Descripcion = "Facturas", TipoDocumento = TipoDocumentoSerie.FacturaVenta, Activa = true,
            UltimoNumeroUsado = "FV-000012", ProximoNumero = "FV-000013", Aviso = "La serie FV alcanzó su número de aviso: quedan 2 número(s).",
            EnAviso = true, Usada = true, Asignada = true, Xmin = 3,
        };
        var otra = new SerieResponse { Id = IdOtra, Codigo = "FV2", Descripcion = "Sucursal", TipoDocumento = TipoDocumentoSerie.FacturaVenta, Activa = true, Xmin = 1 };
        var series = app.Simular<ISerieApiClient>();
        series.Setup(c => c.ListAsync(It.IsAny<SerieFiltro?>(), It.IsAny<PageRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<SerieResponse>([serie, otra], 1, 50, 2));
        series.Setup(c => c.ActivasDelTipoAsync(It.IsAny<TipoDocumentoSerie>(), It.IsAny<CancellationToken>())).ReturnsAsync([serie, otra]);
        series.Setup(c => c.GetAsync(IdSerie, It.IsAny<CancellationToken>())).ReturnsAsync(new SerieDetalleResponse(serie,
        [
            new LineaSerieResponse { Id = IdLinea, SerieId = IdSerie, NumeroInicial = "FV-000001", NumeroFinal = "FV-000014", NumeroAviso = "FV-000012",
                UltimoNumeroUsado = "FV-000012", FechaInicial = new DateOnly(2020, 1, 1), Incremento = 1, Usada = true, Xmin = 8 },
        ]));
        app.Simular<IConfiguracionNumeracionApiClient>()
            .Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ConfiguracionNumeracionResponse { TipoDocumento = TipoDocumentoSerie.FacturaVenta, SerieId = IdSerie, SerieCodigo = "FV", SerieActiva = true, Xmin = 4 },
                new ConfiguracionNumeracionResponse { TipoDocumento = TipoDocumentoSerie.Cobro, SerieId = IdSerie, SerieCodigo = "COBRO", SerieActiva = true, Xmin = 7 },
            ]);
        return app;
    }
}
