using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using OpenSource1.Application.Features.Series.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Inventario;
using OpenSource1.Core.Entities.Ventas;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

/// <summary>
/// <c>api/series</c> (spec no-series, Parte 3) contra Postgres real: CRUD de series y líneas con prefijo, vista previa, protecciones
/// de series usadas/asignadas, solapamiento entre series del mismo tipo (Review Focus 1), xmin y permisos (solo el Administrador
/// escribe). REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SeriesApiTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    private const string Ruta = "/api/series";
    private static readonly DateOnly Desde = new(2020, 1, 1);
    private readonly HttpClient _client = fixture.CreateFactory().CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Theory]
    [InlineData("Supervisor")]
    [InlineData("Ejecutor")]
    public async Task SupervisorYEjecutor_Consultan_PeroNoEscriben_403(string rol)
    {
        var client = Rol(rol);
        var id = SerieFacturaVentaIds.SeriePosteadaId;

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Ruta}?tipo=2")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Ruta}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Ruta}/{id}/proximo")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Ruta}/{id}/lineas")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Ruta, new { codigo = "X", descripcion = "X", tipoDocumento = 8 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"{Ruta}/{id}", new { codigo = "FV", descripcion = "X", tipoDocumento = 2, activa = true, xmin = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"{Ruta}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"{Ruta}/{id}/lineas", new { numeroInicial = "1", numeroFinal = "9", fechaInicial = Desde })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"{Ruta}/{id}/lineas/{SerieFacturaVentaIds.LineaSeriePosteadaId}",
            new { numeroInicial = "00000001", numeroFinal = "99999999", fechaInicial = Desde, xmin = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"{Ruta}/{id}/lineas/{SerieFacturaVentaIds.LineaSeriePosteadaId}")).StatusCode);
    }

    [Fact]
    public async Task Administrador_CrudDeSerieYLineas_ConPrefijo_ProximoYAviso()
    {
        var admin = Rol("Administrador");
        var prefijo = Prefijo();
        var serie = await CrearSerieAsync(admin, TipoDocumentoSerie.DiarioInventario);
        Assert.True(serie.Activa);

        var linea = await CrearLineaAsync(admin, serie.Id, new { numeroInicial = $"{prefijo}0001", numeroFinal = $"{prefijo}9999", fechaInicial = Desde, numeroAviso = $"{prefijo}0001" });
        Assert.Equal(("", false, 1), (linea.UltimoNumeroUsado, linea.Usada, linea.Incremento));

        var proximo = (await admin.GetFromJsonAsync<ProximoNumeroResponse>($"{Ruta}/{serie.Id}/proximo"))!;
        Assert.Equal($"{prefijo}0001", proximo.Numero);
        Assert.Contains("número de aviso", proximo.Aviso);

        var listado = (await admin.GetFromJsonAsync<PagedResult<SerieResponse>>($"{Ruta}?tipo=8&codigo={serie.Codigo}"))!;
        var fila = Assert.Single(listado.Items);
        Assert.Equal(($"{prefijo}0001", true, false, false, "Diario de inventario"), (fila.ProximoNumero, fila.EnAviso, fila.Usada, fila.Asignada, fila.TipoNombre));

        // Modificar la línea (sin usar: todo se puede cambiar) y la cabecera.
        var editada = await admin.PutAsJsonAsync($"{Ruta}/{serie.Id}/lineas/{linea.Id}",
            new { numeroInicial = $"{prefijo}0010", numeroFinal = $"{prefijo}0500", fechaInicial = Desde, xmin = linea.Xmin, incremento = 10 });
        Assert.True(editada.StatusCode == HttpStatusCode.OK, await editada.Content.ReadAsStringAsync());
        Assert.Equal($"{prefijo}0010", (await admin.GetFromJsonAsync<ProximoNumeroResponse>($"{Ruta}/{serie.Id}/proximo"))!.Numero);
        var cabecera = await admin.PutAsJsonAsync($"{Ruta}/{serie.Id}",
            new { codigo = serie.Codigo, descripcion = "Renombrada", tipoDocumento = 8, permiteHuecos = true, activa = true, xmin = serie.Xmin });
        Assert.True(cabecera.StatusCode == HttpStatusCode.OK, await cabecera.Content.ReadAsStringAsync());

        // Borrar: la línea sin usar y la serie sin uso.
        var detalle = (await admin.GetFromJsonAsync<SerieDetalleResponse>($"{Ruta}/{serie.Id}"))!;
        Assert.Equal(("Renombrada", true), (detalle.Serie.Descripcion, detalle.Serie.PermiteHuecos));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Ruta}/{serie.Id}/lineas/{linea.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Ruta}/{serie.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Ruta}/{serie.Id}")).StatusCode);
    }

    [Fact]
    public async Task Validaciones_DeCabeceraYLinea_400_YCodigoDuplicado_409()
    {
        var admin = Rol("Administrador");
        await AssertErrorAsync(await admin.PostAsJsonAsync(Ruta, new { codigo = "mal codigo!", descripcion = "", tipoDocumento = 0 }),
            HttpStatusCode.BadRequest, "Codigo");
        var serie = await CrearSerieAsync(admin, TipoDocumentoSerie.DiarioInventario);
        await AssertErrorAsync(await admin.PostAsJsonAsync(Ruta, new { codigo = serie.Codigo, descripcion = "Otra", tipoDocumento = 8 }),
            HttpStatusCode.Conflict, "Codigo");

        var p = Prefijo();
        await AssertErrorAsync(await admin.PostAsJsonAsync($"{Ruta}/{serie.Id}/lineas", new { numeroInicial = $"{p}001", numeroFinal = $"{p}0999", fechaInicial = Desde }),
            HttpStatusCode.BadRequest, "NumeroFinal");
        await AssertErrorAsync(await admin.PostAsJsonAsync($"{Ruta}/{serie.Id}/lineas", new { numeroInicial = $"{p}001", numeroFinal = $"{p}999", fechaInicial = Desde, incremento = 0 }),
            HttpStatusCode.BadRequest, "Incremento");
        await CrearLineaAsync(admin, serie.Id, new { numeroInicial = $"{p}001", numeroFinal = $"{p}999", fechaInicial = Desde });
        await AssertErrorAsync(await admin.PostAsJsonAsync($"{Ruta}/{serie.Id}/lineas", new { numeroInicial = $"{p}1001", numeroFinal = $"{p}1999", fechaInicial = Desde }),
            HttpStatusCode.BadRequest, "FechaInicial");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"{Ruta}/{Guid.NewGuid()}/lineas", new { numeroInicial = "1", numeroFinal = "9", fechaInicial = Desde })).StatusCode);
    }

    [Fact]
    public async Task Linea_SolapadaConOtraSerieDelMismoTipo_400()
    {
        var admin = Rol("Administrador");
        var serie = await CrearSerieAsync(admin, TipoDocumentoSerie.FacturaVenta);

        // Mismo formato que FV (8 dígitos, sin prefijo) y rango cruzado: una factura repetiría número.
        var respuesta = await admin.PostAsJsonAsync($"{Ruta}/{serie.Id}/lineas", new { numeroInicial = "50000000", numeroFinal = "50000100", fechaInicial = Desde });

        var cuerpo = await AssertErrorAsync(respuesta, HttpStatusCode.BadRequest, "NumeroInicial");
        Assert.Contains("FV", cuerpo.RootElement.GetRawText());

        // Con otro prefijo el mismo rango numérico no se solapa.
        var p = Prefijo();
        var otra = await CrearLineaAsync(admin, serie.Id, new { numeroInicial = $"{p}00000001", numeroFinal = $"{p}00009999", fechaInicial = Desde });
        Assert.Equal($"{p}00000001", otra.NumeroInicial);
    }

    /// <summary>
    /// Final review 2: datos heredados solapados (por SQL) no impiden bloquear o editar una línea sin cambiar su rango; el control de
    /// solapamiento solo corre si cambia el número inicial o el final.
    /// </summary>
    [Fact]
    public async Task LineaHeredadaSolapada_SinCambiarElRango_SeBloquea_YCambiarElRangoSigueValidando()
    {
        var admin = Rol("Administrador");
        var p = Prefijo();
        var serieA = await CrearSerieAsync(admin, TipoDocumentoSerie.DiarioInventario);
        var serieB = await CrearSerieAsync(admin, TipoDocumentoSerie.DiarioInventario);
        await CrearLineaAsync(admin, serieA.Id, new { numeroInicial = $"{p}0001", numeroFinal = $"{p}0100", fechaInicial = Desde });
        var lineaB = await CrearLineaAsync(admin, serieB.Id, new { numeroInicial = $"{p}0200", numeroFinal = $"{p}0300", fechaInicial = Desde });
        await using (var conexion = new NpgsqlConnection(fixture.AppConnectionString))
        {
            await conexion.ExecuteAsync("""UPDATE "LineasSerie" SET "NumeroInicial" = @I WHERE "Id" = @Id""", new { I = $"{p}0050", lineaB.Id });
        }

        var xmin = Assert.Single((await admin.GetFromJsonAsync<SerieDetalleResponse>($"{Ruta}/{serieB.Id}"))!.Lineas).Xmin;
        var bloquea = await admin.PutAsJsonAsync($"{Ruta}/{serieB.Id}/lineas/{lineaB.Id}",
            new { numeroInicial = $"{p}0050", numeroFinal = $"{p}0300", fechaInicial = Desde, xmin, bloqueada = true });
        Assert.True(bloquea.StatusCode == HttpStatusCode.OK, await bloquea.Content.ReadAsStringAsync());
        var bloqueada = Assert.Single((await admin.GetFromJsonAsync<SerieDetalleResponse>($"{Ruta}/{serieB.Id}"))!.Lineas);
        Assert.True(bloqueada.Bloqueada);

        await AssertErrorAsync(await admin.PutAsJsonAsync($"{Ruta}/{serieB.Id}/lineas/{lineaB.Id}",
            new { numeroInicial = $"{p}0050", numeroFinal = $"{p}0400", fechaInicial = Desde, xmin = bloqueada.Xmin, bloqueada = true }),
            HttpStatusCode.BadRequest, "NumeroInicial");
    }

    [Fact]
    public async Task CambiarTipo_ConLineasQueSeSolapan_400()
    {
        var admin = Rol("Administrador");
        // Tipo Cliente: SOCIOS tiene 6 dígitos, así que una línea de 8 dígitos sin prefijo no choca en su tipo, pero sí con FV.
        var serie = await CrearSerieAsync(admin, TipoDocumentoSerie.Cliente);
        await CrearLineaAsync(admin, serie.Id, new { numeroInicial = "70000001", numeroFinal = "70000100", fechaInicial = Desde });

        var respuesta = await admin.PutAsJsonAsync($"{Ruta}/{serie.Id}",
            new { codigo = serie.Codigo, descripcion = "X", tipoDocumento = (short)TipoDocumentoSerie.FacturaVenta, permiteHuecos = false, activa = true, xmin = serie.Xmin });

        await AssertErrorAsync(respuesta, HttpStatusCode.BadRequest, "TipoDocumento");
    }

    [Fact]
    public async Task SerieYLineaUsadas_Protegidas_ElFinalSoloCrece()
    {
        var admin = Rol("Administrador");
        var p = Prefijo();
        var serie = await CrearSerieAsync(admin, TipoDocumentoSerie.DiarioInventario);
        var linea = await CrearLineaAsync(admin, serie.Id, new { numeroInicial = $"{p}0001", numeroFinal = $"{p}0100", fechaInicial = Desde, ultimoNumeroUsado = $"{p}0005" });
        Assert.True(linea.Usada);

        await AssertErrorAsync(await admin.DeleteAsync($"{Ruta}/{serie.Id}"), HttpStatusCode.Conflict, "Id");
        await AssertErrorAsync(await admin.DeleteAsync($"{Ruta}/{serie.Id}/lineas/{linea.Id}"), HttpStatusCode.Conflict, "Id");
        await AssertErrorAsync(await admin.PutAsJsonAsync($"{Ruta}/{serie.Id}/lineas/{linea.Id}",
            new { numeroInicial = $"{p}0002", numeroFinal = $"{p}0100", fechaInicial = Desde, xmin = linea.Xmin }), HttpStatusCode.Conflict, "NumeroInicial");
        await AssertErrorAsync(await admin.PutAsJsonAsync($"{Ruta}/{serie.Id}/lineas/{linea.Id}",
            new { numeroInicial = $"{p}0001", numeroFinal = $"{p}0050", fechaInicial = Desde, xmin = linea.Xmin }), HttpStatusCode.BadRequest, "NumeroFinal");
        var crece = await admin.PutAsJsonAsync($"{Ruta}/{serie.Id}/lineas/{linea.Id}",
            new { numeroInicial = $"{p}0001", numeroFinal = $"{p}0500", fechaInicial = Desde, xmin = linea.Xmin, bloqueada = true });
        Assert.True(crece.StatusCode == HttpStatusCode.OK, await crece.Content.ReadAsStringAsync());
        await AssertErrorAsync(await admin.PutAsJsonAsync($"{Ruta}/{serie.Id}",
            new { codigo = serie.Codigo, descripcion = "X", tipoDocumento = 5, permiteHuecos = false, activa = true, xmin = serie.Xmin }),
            HttpStatusCode.Conflict, "TipoDocumento");

        // Obsoleto: el xmin de la línea ya cambió.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"{Ruta}/{serie.Id}/lineas/{linea.Id}",
            new { numeroInicial = $"{p}0001", numeroFinal = $"{p}0600", fechaInicial = Desde, xmin = linea.Xmin })).StatusCode);
    }

    [Fact]
    public async Task NSS1_InicialCero_AvisoFueraDeRango_YFormatoDeLineaUsada_400()
    {
        var admin = Rol("Administrador");
        var p = Prefijo();
        var serie = await CrearSerieAsync(admin, TipoDocumentoSerie.DiarioInventario);
        await AssertErrorAsync(await admin.PostAsJsonAsync($"{Ruta}/{serie.Id}/lineas", new { numeroInicial = $"{p}0000", numeroFinal = $"{p}0100", fechaInicial = Desde }),
            HttpStatusCode.BadRequest, "NumeroInicial");
        await AssertErrorAsync(await admin.PostAsJsonAsync($"{Ruta}/{serie.Id}/lineas", new { numeroInicial = $"{p}0001", numeroFinal = $"{p}0100", fechaInicial = Desde, numeroAviso = $"{p}0101" }),
            HttpStatusCode.BadRequest, "NumeroAviso");
        await AssertErrorAsync(await admin.PostAsJsonAsync($"{Ruta}/{serie.Id}/lineas", new { numeroInicial = $"{p}0001", numeroFinal = $"{p}0100", fechaInicial = Desde, numeroAviso = "0050" }),
            HttpStatusCode.BadRequest, "NumeroAviso");

        var linea = await CrearLineaAsync(admin, serie.Id, new { numeroInicial = $"{p}0001", numeroFinal = $"{p}0100", fechaInicial = Desde, ultimoNumeroUsado = $"{p}0010" });

        // Usada: el final no cambia de ancho ni de prefijo, y el aviso debe compartir el formato del rango.
        await AssertErrorAsync(await admin.PutAsJsonAsync($"{Ruta}/{serie.Id}/lineas/{linea.Id}",
            new { numeroInicial = $"{p}0001", numeroFinal = $"{p}00200", fechaInicial = Desde, xmin = linea.Xmin }), HttpStatusCode.BadRequest, "NumeroFinal");
        await AssertErrorAsync(await admin.PutAsJsonAsync($"{Ruta}/{serie.Id}/lineas/{linea.Id}",
            new { numeroInicial = $"{p}0001", numeroFinal = $"{p}0200", fechaInicial = Desde, xmin = linea.Xmin, numeroAviso = "X-0150" }), HttpStatusCode.BadRequest, "NumeroAviso");

        // Aviso, final mayor y bloqueo sí; el contador se conserva.
        var ok = await admin.PutAsJsonAsync($"{Ruta}/{serie.Id}/lineas/{linea.Id}",
            new { numeroInicial = $"{p}0001", numeroFinal = $"{p}0200", fechaInicial = Desde, xmin = linea.Xmin, numeroAviso = $"{p}0150", bloqueada = true });
        Assert.True(ok.StatusCode == HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
        var lineas = (await admin.GetFromJsonAsync<List<LineaSerieResponse>>($"{Ruta}/{serie.Id}/lineas"))!;
        var editada = Assert.Single(lineas);
        Assert.Equal(($"{p}0010", $"{p}0200", $"{p}0150", true, true), (editada.UltimoNumeroUsado, editada.NumeroFinal, editada.NumeroAviso, editada.Bloqueada, editada.Usada));

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Ruta}/{Guid.NewGuid()}/proximo")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"{Ruta}/{serie.Id}/lineas/{Guid.NewGuid()}",
            new { numeroInicial = "1", numeroFinal = "9", fechaInicial = Desde, xmin = 1 })).StatusCode);
    }

    [Fact]
    public async Task SerieAsignadaEnLaConfiguracion_NoSeEliminaNiSeDesactiva_409()
    {
        var admin = Rol("Administrador");
        var serie = await CrearSerieAsync(admin, TipoDocumentoSerie.Cobro);
        await using var conexion = new NpgsqlConnection(fixture.AppConnectionString);
        var original = await conexion.ExecuteScalarAsync<Guid>("""SELECT "SerieId" FROM "ConfiguracionesNumeracion" WHERE "TipoDocumento" = 5""");
        try
        {
            await conexion.ExecuteAsync("""UPDATE "ConfiguracionesNumeracion" SET "SerieId" = @S WHERE "TipoDocumento" = 5""", new { S = serie.Id });

            await AssertErrorAsync(await admin.DeleteAsync($"{Ruta}/{serie.Id}"), HttpStatusCode.Conflict, "Id");
            await AssertErrorAsync(await admin.PutAsJsonAsync($"{Ruta}/{serie.Id}",
                new { codigo = serie.Codigo, descripcion = "X", tipoDocumento = 5, permiteHuecos = false, activa = false, xmin = serie.Xmin }),
                HttpStatusCode.Conflict, "Activa");
            await AssertErrorAsync(await admin.PutAsJsonAsync($"{Ruta}/{serie.Id}",
                new { codigo = serie.Codigo, descripcion = "X", tipoDocumento = 8, permiteHuecos = false, activa = true, xmin = serie.Xmin }),
                HttpStatusCode.Conflict, "TipoDocumento");
            Assert.True((await admin.GetFromJsonAsync<SerieDetalleResponse>($"{Ruta}/{serie.Id}"))!.Serie.Asignada);
        }
        finally
        {
            await conexion.ExecuteAsync("""UPDATE "ConfiguracionesNumeracion" SET "SerieId" = @S WHERE "TipoDocumento" = 5""", new { S = original });
        }
    }

    [Theory]
    [InlineData("ABC")]
    [InlineData("7")]
    [InlineData("{p}00001")]
    public async Task ContadorIncoherenteOHeredado_CuentaComoUsado_EnTodasLasProtecciones(string contador)
    {
        // "Usada" tiene una sola definición (CalculoNumeroSerie.EstaUsada, falla cerrado): un contador que no se interpreta, con otro
        // prefijo o más ancho que la línea protege la serie igual que la línea (antes el SQL solo comparaba los dígitos finales).
        var admin = Rol("Administrador");
        var p = Prefijo();
        var serie = await CrearSerieAsync(admin, TipoDocumentoSerie.DiarioInventario);
        var linea = await CrearLineaAsync(admin, serie.Id, new { numeroInicial = $"{p}0010", numeroFinal = $"{p}0100", fechaInicial = Desde });
        await using (var conexion = new NpgsqlConnection(fixture.AppConnectionString))
        {
            await conexion.ExecuteAsync("""UPDATE "LineasSerie" SET "UltimoNumeroUsado" = @C WHERE "Id" = @Id""",
                new { C = contador.Replace("{p}", p, StringComparison.Ordinal), linea.Id });
        }

        var detalle = (await admin.GetFromJsonAsync<SerieDetalleResponse>($"{Ruta}/{serie.Id}"))!;
        Assert.Equal((true, true), (detalle.Serie.Usada, Assert.Single(detalle.Lineas).Usada));
        var fila = Assert.Single((await admin.GetFromJsonAsync<PagedResult<SerieResponse>>($"{Ruta}?tipo=8&codigo={serie.Codigo}"))!.Items);
        Assert.True(fila.Usada);

        await AssertErrorAsync(await admin.DeleteAsync($"{Ruta}/{serie.Id}"), HttpStatusCode.Conflict, "Id");
        await AssertErrorAsync(await admin.PutAsJsonAsync($"{Ruta}/{serie.Id}",
            new { codigo = serie.Codigo, descripcion = "X", tipoDocumento = 5, permiteHuecos = false, activa = true, xmin = serie.Xmin }),
            HttpStatusCode.Conflict, "TipoDocumento");
        await AssertErrorAsync(await admin.DeleteAsync($"{Ruta}/{serie.Id}/lineas/{linea.Id}"), HttpStatusCode.Conflict, "Id");
    }

    [Fact]
    public async Task SerieUsadaPorPlantillaDeDiario_NoSeEliminaNiCambiaDeTipo_409()
    {
        var admin = Rol("Administrador");
        var serie = await CrearSerieAsync(admin, TipoDocumentoSerie.DiarioInventario);
        await using var conexion = new NpgsqlConnection(fixture.AppConnectionString);
        var original = await conexion.ExecuteScalarAsync<Guid>("""SELECT "SerieId" FROM "PlantillasDiario" WHERE "Id" = @Id""", new { Id = PlantillaDiarioIds.Articulo });
        try
        {
            await conexion.ExecuteAsync("""UPDATE "PlantillasDiario" SET "SerieId" = @S WHERE "Id" = @Id""", new { S = serie.Id, Id = PlantillaDiarioIds.Articulo });

            var borrar = await AssertErrorAsync(await admin.DeleteAsync($"{Ruta}/{serie.Id}"), HttpStatusCode.Conflict, "Id");
            Assert.Contains("plantillas o lotes", borrar.RootElement.GetRawText());
            await AssertErrorAsync(await admin.PutAsJsonAsync($"{Ruta}/{serie.Id}",
                new { codigo = serie.Codigo, descripcion = "X", tipoDocumento = 7, permiteHuecos = false, activa = true, xmin = serie.Xmin }),
                HttpStatusCode.Conflict, "TipoDocumento");
        }
        finally
        {
            await conexion.ExecuteAsync("""UPDATE "PlantillasDiario" SET "SerieId" = @S WHERE "Id" = @Id""", new { S = original, Id = PlantillaDiarioIds.Articulo });
        }
    }

    [Fact]
    public async Task Cabecera_ConXminObsoleto_409()
    {
        var admin = Rol("Administrador");
        var serie = await CrearSerieAsync(admin, TipoDocumentoSerie.DiarioInventario);
        var cuerpo = new { codigo = serie.Codigo, descripcion = "Primera", tipoDocumento = 8, permiteHuecos = false, activa = true, xmin = serie.Xmin };

        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"{Ruta}/{serie.Id}", cuerpo)).StatusCode);
        var obsoleta = await admin.PutAsJsonAsync($"{Ruta}/{serie.Id}", cuerpo with { descripcion = "Segunda" });

        Assert.Equal(HttpStatusCode.Conflict, obsoleta.StatusCode);
        Assert.Equal("Primera", (await admin.GetFromJsonAsync<SerieDetalleResponse>($"{Ruta}/{serie.Id}"))!.Serie.Descripcion);
    }

    [Fact]
    public async Task LineasBorradas_NoCuentanParaSolapamientoNiFecha()
    {
        var admin = Rol("Administrador");
        var p = Prefijo();
        var a = await CrearSerieAsync(admin, TipoDocumentoSerie.DiarioInventario);
        var b = await CrearSerieAsync(admin, TipoDocumentoSerie.DiarioInventario);
        var rango = new { numeroInicial = $"{p}0001", numeroFinal = $"{p}0100", fechaInicial = Desde };

        var borrada = await CrearLineaAsync(admin, a.Id, rango);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Ruta}/{a.Id}/lineas/{borrada.Id}")).StatusCode);

        // Mismo rango en otra serie del tipo y misma fecha en la serie A: la línea borrada ya no cuenta.
        await CrearLineaAsync(admin, b.Id, rango);
        await CrearLineaAsync(admin, a.Id, new { numeroInicial = $"{p}0200", numeroFinal = $"{p}0300", fechaInicial = Desde });

        // Al borrar la serie B se borran sus líneas: el rango vuelve a quedar libre.
        var borrarB = await admin.DeleteAsync($"{Ruta}/{b.Id}");
        Assert.True(borrarB.StatusCode == HttpStatusCode.NoContent, await borrarB.Content.ReadAsStringAsync());
        await CrearLineaAsync(admin, a.Id, rango with { fechaInicial = Desde.AddYears(1) });
    }

    [Fact]
    public async Task LineaDeOtraSerie_404_YNoSeToca()
    {
        var admin = Rol("Administrador");
        var p = Prefijo();
        var a = await CrearSerieAsync(admin, TipoDocumentoSerie.DiarioInventario);
        var b = await CrearSerieAsync(admin, TipoDocumentoSerie.DiarioInventario);
        var lineaB = await CrearLineaAsync(admin, b.Id, new { numeroInicial = $"{p}0001", numeroFinal = $"{p}0100", fechaInicial = Desde });

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"{Ruta}/{a.Id}/lineas/{lineaB.Id}",
            new { numeroInicial = $"{p}0001", numeroFinal = $"{p}0200", fechaInicial = Desde, xmin = lineaB.Xmin })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"{Ruta}/{a.Id}/lineas/{lineaB.Id}")).StatusCode);

        var intacta = Assert.Single((await admin.GetFromJsonAsync<SerieDetalleResponse>($"{Ruta}/{b.Id}"))!.Lineas);
        Assert.Equal(($"{p}0100", lineaB.Xmin), (intacta.NumeroFinal, intacta.Xmin));
    }

    [Fact]
    public async Task AltasConcurrentesConElMismoCodigo_UnaGana_ElResto409ConCodigo()
    {
        await using var factory = fixture.CreateFactory();
        var codigo = $"C{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var clientes = Enumerable.Range(0, 6).Select(_ =>
        {
            var c = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            c.DefaultRequestHeaders.Add("X-Test-User", "administrador");
            c.DefaultRequestHeaders.Add("X-Test-Roles", "Administrador");
            return c;
        }).ToList();

        var respuestas = await Task.WhenAll(clientes.Select(c =>
            c.PostAsJsonAsync(Ruta, new { codigo, descripcion = "Concurrente", tipoDocumento = (short)TipoDocumentoSerie.DiarioInventario })));

        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Created);
        foreach (var perdedora in respuestas.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            var cuerpo = await AssertErrorAsync(perdedora, HttpStatusCode.Conflict, "Codigo");
            Assert.Contains($"Ya existe una serie con el código {codigo}", cuerpo.RootElement.GetRawText());
        }
    }

    private static string Prefijo() => $"T{Guid.NewGuid():N}"[..5].ToUpperInvariant() + "-";

    private static async Task<SerieResponse> CrearSerieAsync(HttpClient client, TipoDocumentoSerie tipo)
    {
        var codigo = $"S{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var respuesta = await client.PostAsJsonAsync(Ruta, new { codigo, descripcion = $"Serie {codigo}", tipoDocumento = (short)tipo });
        Assert.True(respuesta.StatusCode == HttpStatusCode.Created, await respuesta.Content.ReadAsStringAsync());
        return (await respuesta.Content.ReadFromJsonAsync<SerieResponse>())!;
    }

    private static async Task<LineaSerieResponse> CrearLineaAsync(HttpClient client, Guid serieId, object cuerpo)
    {
        var respuesta = await client.PostAsJsonAsync($"{Ruta}/{serieId}/lineas", cuerpo);
        Assert.True(respuesta.StatusCode == HttpStatusCode.Created, await respuesta.Content.ReadAsStringAsync());
        return (await respuesta.Content.ReadFromJsonAsync<LineaSerieResponse>())!;
    }

    private static async Task<JsonDocument> AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string campo)
    {
        var cuerpo = await response.Content.ReadAsStringAsync();
        Assert.True(status == response.StatusCode, $"Se esperaba {status} y llegó {response.StatusCode}: {cuerpo}");
        var json = JsonDocument.Parse(cuerpo);
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty(campo, out _), $"Se esperaba el campo '{campo}': {cuerpo}");
        return json;
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
