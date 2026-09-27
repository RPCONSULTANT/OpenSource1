using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.CategoriasProducto.Dtos;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Application.Features.UnidadesMedida.Dtos;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Enums;
using OpenSource1.Infrastructure.Data;
using OpenSource1.Core.Common;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

[Collection(PostgresCollection.Name)]
public sealed class ProductosApiTests : IClassFixture<PostgresTestFixture>
{
    private static readonly Guid AlmacenPrincipal = Guid.Parse("b1000000-0000-0000-0000-000000000001");
    private static readonly Guid UnidadUnd = Guid.Parse("a1000000-0000-0000-0000-000000000001");

    private readonly PostgresTestFixture _fixture;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ProductosApiTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
        _factory = fixture.CreateFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    /// <summary>
    /// Registra una entrada con <see cref="IRegistroMovimientosInventario"/> (Task 3.4), en su propio scope/transacción,
    /// para que los tests de <c>existencia</c>/<c>stockState</c>/<c>/existencias</c> tengan movimientos reales del libro.
    /// </summary>
    private async Task RegistrarEntradaAsync(Guid productoId, decimal cantidad, decimal costoUnitario = 1m, Guid? almacenId = null, Guid? unidadMedidaId = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var sesion = scope.ServiceProvider.GetRequiredService<IDbSession>();
        var registro = scope.ServiceProvider.GetRequiredService<IRegistroMovimientosInventario>();

        await using var tx = await sesion.BeginTransactionAsync();
        var fecha = DateOnly.FromDateTime(DateTime.UtcNow);
        var solicitud = new MovimientoInventarioSolicitud(
            productoId, almacenId ?? AlmacenPrincipal, TipoMovimientoInventario.Compra, cantidad, EsEntrada: true, unidadMedidaId ?? UnidadUnd, costoUnitario,
            fecha, fecha, TipoDocumentoInventario.RegistroDiario, "DOC-TEST", 1, TipoOrigenMovimiento.Diario, $"T-{Guid.NewGuid():N}");
        var resultado = await registro.RegistrarAsync(solicitud);
        Assert.True(resultado.EsExito, resultado.EsFallo ? string.Join("; ", resultado.Errores.Select(e => $"{e.Codigo}: {e.Mensaje}")) : string.Empty);
        await sesion.CommitAsync();
    }

    [Fact]
    public async Task Crud_And_Auth_Are_Wired()
    {
        var anon = new HttpRequestMessage(HttpMethod.Get, "/api/productos");
        anon.Headers.Add("X-Test-Anonymous", "true");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(anon)).StatusCode);

        var forbiddenClient = CreateClient("Supervisor");
        var forbidden = await forbiddenClient.PostAsJsonAsync("/api/productos", new { codigo = $"P-{Guid.NewGuid():N}", nombre = "Prod", precioVenta = 10.5m, stock = 5 });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var client = CreateClient("Administrador");
        var codigo = $"P-{Guid.NewGuid():N}";
        var create = await client.PostAsJsonAsync("/api/productos", new { codigo, nombre = "Prod", precioVenta = 10.5m, stock = 5 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        var list = await client.GetAsync("/api/productos");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var paged = await list.Content.ReadFromJsonAsync<PagedResult<ProductoResponse>>();
        Assert.NotNull(paged);
        Assert.Contains(paged!.Items, p => p.Id == created);
        Assert.True(paged.Total >= 1);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/productos/{created}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/productos/{created}", new { codigo, nombre = "Prod 2", precioVenta = 11m, stock = 7 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/productos/{Guid.NewGuid()}")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Supervisor").DeleteAsync($"/api/productos/{created}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateClient("Ejecutor").DeleteAsync($"/api/productos/{created}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await CreateClient("Administrador").DeleteAsync($"/api/productos/{created}")).StatusCode);
    }

    [Fact]
    public async Task List_RespetaElTamanoDePaginaYDevuelveElTotal()
    {
        var client = CreateClient("Administrador");

        for (var i = 0; i < 3; i++)
        {
            var codigo = $"PAG-{Guid.NewGuid():N}";
            var create = await client.PostAsJsonAsync("/api/productos", new { codigo, nombre = $"ProdPag{i}", precioVenta = 1m, stock = 1 });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        }

        var response = await client.GetAsync("/api/productos?tamanoPagina=2&pagina=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var paged = await response.Content.ReadFromJsonAsync<PagedResult<ProductoResponse>>();
        Assert.NotNull(paged);
        Assert.Equal(2, paged!.Items.Count);
        Assert.True(paged.Total >= 3);
        Assert.True(paged.TotalPaginas >= 2);
    }

    [Fact]
    public async Task List_FiltroDePrecioInvalido_Devuelve400SinLanzar()
    {
        var client = CreateClient("Administrador");

        var response = await client.GetAsync("/api/productos?precioVenta=no-es-un-numero");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_TrasBorrarElMismoCodigo_NoChocaConElIndiceUnico_YDevuelve201()
    {
        // Hallazgo 1 (índice único con soft delete): verificado
        // contra Postgres real que borrar (soft delete) y recrear con el mismo Codigo ya no
        // choca contra "IX_Productos_Codigo" gracias al índice único parcial "IsDeleted = false".
        var client = CreateClient("Administrador");
        var codigo = $"DEL-{Guid.NewGuid():N}";

        var create = await client.PostAsJsonAsync("/api/productos", new { codigo, nombre = "Original", precioVenta = 10m, stock = 1 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var createdId = JsonDocument.Parse(await create.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        var delete = await client.DeleteAsync($"/api/productos/{createdId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var recreate = await client.PostAsJsonAsync("/api/productos", new { codigo, nombre = "Recreado", precioVenta = 12m, stock = 2 });

        Assert.Equal(HttpStatusCode.Created, recreate.StatusCode);
        Assert.NotEqual(HttpStatusCode.InternalServerError, recreate.StatusCode);
    }

    [Fact]
    public async Task Delete_ConMovimientosDeInventario_Devuelve409YSigueVisible_YSinMovimientosDevuelve204()
    {
        // Guarda añadida en la revisión final de la Fase 3 (mismo patrón que DeleteAlmacenCommandHandler): el
        // libro de inventario (append-only, sin FK-cascade de borrado) impide que un producto con movimientos
        // desaparezca, aunque el borrado sea lógico.
        var client = CreateClient("Administrador");
        var conMovimiento = (await (await client.PostAsJsonAsync("/api/productos", new { codigo = CodigoUnico("DELM"), nombre = "Con movimiento", precioVenta = 1m })).Content.ReadFromJsonAsync<ProductoResponse>())!;
        var sinMovimiento = (await (await client.PostAsJsonAsync("/api/productos", new { codigo = CodigoUnico("DELS"), nombre = "Sin movimiento", precioVenta = 1m })).Content.ReadFromJsonAsync<ProductoResponse>())!;
        await RegistrarEntradaAsync(conMovimiento.Id, 5m);

        var borrarConMovimiento = await client.DeleteAsync($"/api/productos/{conMovimiento.Id}");
        Assert.Equal(HttpStatusCode.Conflict, borrarConMovimiento.StatusCode);
        var cuerpo = await borrarConMovimiento.Content.ReadAsStringAsync();
        Assert.Contains("movimientos de inventario", cuerpo);

        // Sigue visible: el 409 no tocó nada.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/productos/{conMovimiento.Id}")).StatusCode);

        var borrarSinMovimiento = await client.DeleteAsync($"/api/productos/{sinMovimiento.Id}");
        Assert.Equal(HttpStatusCode.NoContent, borrarSinMovimiento.StatusCode);
    }

    [Fact]
    public async Task Create_ConCodigoDuplicadoActivo_Devuelve409EnVezDe500()
    {
        // Hallazgo 1, parte 2: ProductosController.Create no hace un chequeo de existencia previo
        // (a diferencia de otros controllers), así que un Codigo duplicado siempre llegó hasta
        // el INSERT y violaba "IX_Productos_Codigo" directamente. Antes del fix a
        // GlobalExceptionHandler, el DbUpdateException resultante (con Npgsql.PostgresException
        // SqlState 23505 como InnerException, confirmado contra Postgres real) no tenía rama
        // dedicada y cae como 500 desnudo. Con el fix, se traduce a 409.
        var client = CreateClient("Administrador");
        var codigo = $"DUP-{Guid.NewGuid():N}";

        var primero = await client.PostAsJsonAsync("/api/productos", new { codigo, nombre = "Primero", precioVenta = 10m, stock = 1 });
        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);

        var duplicado = await client.PostAsJsonAsync("/api/productos", new { codigo, nombre = "Segundo", precioVenta = 10m, stock = 1 });

        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);
        Assert.NotEqual(HttpStatusCode.InternalServerError, duplicado.StatusCode);

        var cuerpo = await duplicado.Content.ReadAsStringAsync();
        Assert.Contains("entidad.codigo_duplicado", cuerpo);
    }

    [Fact]
    public async Task List_FiltroConPorcentajeLiteral_NoLoTrataComoComodinContraPostgresReal()
    {
        // Hallazgo 8: sin escapar los metacaracteres de ILIKE, un "%" literal en el término de
        // búsqueda se interpreta como comodín. Se arma un falso positivo a propósito: sin el fix,
        // buscar "{sufijo}50%off" se traduce en el patrón "%{sufijo}50%off%", cuyo "%" interno
        // hace de comodín y coincide con cualquier cosa que tenga "{sufijo}50" seguido en algún
        // punto por "off" (como "{sufijo}50XXXoff"), no solo con el literal "{sufijo}50%off".
        var client = CreateClient("Administrador");
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var nombreConPorcentajeLiteral = $"{sufijo}50%off";
        var nombreFalsoPositivoSinEscape = $"{sufijo}50XXXoff";

        foreach (var nombre in new[] { nombreConPorcentajeLiteral, nombreFalsoPositivoSinEscape })
        {
            var codigo = $"PCT-{Guid.NewGuid():N}";
            var create = await client.PostAsJsonAsync("/api/productos", new { codigo, nombre, precioVenta = 1m, stock = 1 });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        }

        var filtroCodificado = Uri.EscapeDataString(nombreConPorcentajeLiteral);
        var response = await client.GetAsync($"/api/productos?nombre={filtroCodificado}&tamanoPagina=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var paged = await response.Content.ReadFromJsonAsync<PagedResult<ProductoResponse>>();
        Assert.NotNull(paged);
        Assert.Contains(paged!.Items, p => p.Nombre == nombreConPorcentajeLiteral);
        Assert.DoesNotContain(paged.Items, p => p.Nombre == nombreFalsoPositivoSinEscape);
    }

    [Fact]
    public async Task List_ColumnaDeOrdenNoPermitida_CaeAlOrdenPorDefectoSinRomper()
    {
        var client = CreateClient("Administrador");

        var response = await client.GetAsync("/api/productos?ordenarPor=" + Uri.EscapeDataString("\"; DROP TABLE \"Productos") + "&tamanoPagina=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await response.Content.ReadFromJsonAsync<PagedResult<ProductoResponse>>();
        Assert.NotNull(paged);
    }

    // ---- Task 2.9: categoría y unidad base por FK, campos nuevos, filtros y órdenes sobre el JOIN aplanado ----

    [Fact]
    public async Task Create_ConTodosLosCamposNuevos_LosDevuelveYLosGuarda_YCostoUnitarioYCostoAjustadoLosMantieneElSistema()
    {
        var client = CreateClient("Administrador");
        var categoria = await CrearCategoriaAsync(client);
        var unidad = await CrearUnidadAsync(client);

        var create = await client.PostAsJsonAsync("/api/productos", new
        {
            codigo = CodigoUnico("NEW"), nombre = "Completo", precioVenta = 12.3456m, stock = 4,
            categoriaId = categoria.Id, unidadMedidaBaseId = unidad.Id, metodoCosteo = 1, costoEstandar = 7.5m, bloqueado = 1,
            // Los mantiene el sistema: el cuerpo NO los acepta (se ignoran).
            costoUnitario = 99m, costoAjustado = false
        });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var creado = (await create.Content.ReadFromJsonAsync<ProductoResponse>())!;
        Assert.Equal(12.3456m, creado.PrecioVenta);
        Assert.Equal(categoria.Id, creado.CategoriaId);
        Assert.Equal(categoria.Codigo, creado.CategoriaCodigo);
        Assert.Equal(categoria.Nombre, creado.CategoriaNombre);
        Assert.Equal(unidad.Id, creado.UnidadMedidaBaseId);
        Assert.Equal(unidad.Codigo, creado.UnidadMedidaCodigo);
        Assert.Equal(unidad.Nombre, creado.UnidadMedidaNombre);
        Assert.Equal(MetodoCosteo.Promedio, creado.MetodoCosteo);
        Assert.Equal(7.5m, creado.CostoEstandar);
        Assert.Equal(BloqueoProducto.Venta, creado.Bloqueado);
        Assert.Equal(0m, creado.CostoUnitario);
        Assert.True(creado.CostoAjustado);

        var leido = (await client.GetFromJsonAsync<ProductoResponse>($"/api/productos/{creado.Id}"))!;
        Assert.Equal(categoria.Codigo, leido.CategoriaCodigo);
        Assert.Equal(unidad.Nombre, leido.UnidadMedidaNombre);
        Assert.Equal(12.3456m, leido.PrecioVenta);
        Assert.Equal(BloqueoProducto.Venta, leido.Bloqueado);
        Assert.Equal(0m, leido.CostoUnitario);
        Assert.True(leido.CostoAjustado);
    }

    [Fact]
    public async Task Create_SinCategoriaNiUnidad_UsaGeneralYUnd()
    {
        var client = CreateClient("Administrador");

        var create = await client.PostAsJsonAsync("/api/productos", new { codigo = CodigoUnico("DEF"), nombre = "Por defecto", precioVenta = 1m });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var creado = (await create.Content.ReadFromJsonAsync<ProductoResponse>())!;
        Assert.Equal("GENERAL", creado.CategoriaCodigo);
        Assert.Equal("UND", creado.UnidadMedidaCodigo);
        Assert.Equal(BloqueoProducto.Ninguno, creado.Bloqueado);
        Assert.Equal(MetodoCosteo.Promedio, creado.MetodoCosteo);
        Assert.Equal(0m, creado.CostoEstandar);
    }

    [Theory]
    [InlineData("categoriaId", "CategoriaId")]
    [InlineData("unidadMedidaBaseId", "UnidadMedidaBaseId")]
    public async Task Create_ConReferenciaInexistente_Devuelve400ConElCampoDelDto_YNoUn404(string propiedad, string campo)
    {
        var client = CreateClient("Administrador");
        var cuerpo = new Dictionary<string, object?> { ["codigo"] = CodigoUnico("REF"), ["nombre"] = "Ref", [propiedad] = Guid.NewGuid() };

        var respuesta = await client.PostAsJsonAsync("/api/productos", cuerpo);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var errores = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement.GetProperty("errors");
        Assert.True(errores.TryGetProperty(campo, out _), $"El 400 debe señalar {campo}");
    }

    [Theory]
    [InlineData("{\"precioVenta\": -1}", "PrecioVenta")]
    [InlineData("{\"precioVenta\": 1.00001}", "PrecioVenta")]
    [InlineData("{\"costoEstandar\": -0.01}", "CostoEstandar")]
    [InlineData("{\"costoEstandar\": 1.23456}", "CostoEstandar")]
    [InlineData("{\"bloqueado\": 9}", "Bloqueado")]
    [InlineData("{\"metodoCosteo\": 7}", "MetodoCosteo")]
    public async Task Create_ConValoresInvalidos_Devuelve400ConElCampo(string extraJson, string campo)
    {
        var client = CreateClient("Administrador");
        var cuerpo = JsonDocument.Parse(extraJson).RootElement.EnumerateObject().ToDictionary(x => x.Name, x => (object?)x.Value.Clone());
        cuerpo["codigo"] = CodigoUnico("INV");
        cuerpo["nombre"] = "Invalido";

        var respuesta = await client.PostAsJsonAsync("/api/productos", cuerpo);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var errores = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement.GetProperty("errors");
        Assert.True(errores.TryGetProperty(campo, out _), $"El 400 debe señalar {campo}: {errores}");
    }

    [Fact]
    public async Task Update_SoloElNombre_ConservaCategoriaUnidadBloqueoCostosYPrecio()
    {
        var client = CreateClient("Administrador");
        var categoria = await CrearCategoriaAsync(client);
        var unidad = await CrearUnidadAsync(client);
        var create = await client.PostAsJsonAsync("/api/productos", new
        {
            codigo = CodigoUnico("PAR"), nombre = "Original", precioVenta = 33.3333m,
            categoriaId = categoria.Id, unidadMedidaBaseId = unidad.Id, costoEstandar = 5.5m, bloqueado = 2
        });
        var creado = (await create.Content.ReadFromJsonAsync<ProductoResponse>())!;
        // El sistema (rutina de costeo de la Fase 3) mantiene estos dos: se simulan directamente en la BD.
        await EjecutarAsync($"""UPDATE "Productos" SET "CostoUnitario" = 8.75, "CostoAjustado" = false WHERE "Id" = '{creado.Id}'""");

        // PUT parcial: solo codigo y nombre (los demás campos ausentes = conservar).
        var put = await client.PutAsJsonAsync($"/api/productos/{creado.Id}", new { codigo = creado.Codigo, nombre = "Renombrado" });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var tras = (await client.GetFromJsonAsync<ProductoResponse>($"/api/productos/{creado.Id}"))!;
        Assert.Equal("Renombrado", tras.Nombre);
        Assert.Equal(33.3333m, tras.PrecioVenta);
        // Sin movimientos registrados: la existencia derivada (Task 3.6) es 0, no se guarda ni se "conserva" en el maestro.
        Assert.Equal(0m, tras.Existencia);
        Assert.Equal(categoria.Id, tras.CategoriaId);
        Assert.Equal(unidad.Id, tras.UnidadMedidaBaseId);
        Assert.Equal(BloqueoProducto.Todo, tras.Bloqueado);
        Assert.Equal(5.5m, tras.CostoEstandar);
        Assert.Equal(MetodoCosteo.Promedio, tras.MetodoCosteo);
        Assert.Equal(8.75m, tras.CostoUnitario);
        Assert.False(tras.CostoAjustado);
    }

    [Fact]
    public async Task Update_ConValoresInformados_LosAplica_YElPutNoTocaCostoUnitarioNiCostoAjustado()
    {
        var client = CreateClient("Administrador");
        var categoria = await CrearCategoriaAsync(client);
        var unidad = await CrearUnidadAsync(client);
        var creado = (await (await client.PostAsJsonAsync("/api/productos", new { codigo = CodigoUnico("APL"), nombre = "A", precioVenta = 1m })).Content.ReadFromJsonAsync<ProductoResponse>())!;

        var put = await client.PutAsJsonAsync($"/api/productos/{creado.Id}", new
        {
            codigo = creado.Codigo, nombre = "B", precioVenta = 9.9m, stock = 3, categoriaId = categoria.Id, unidadMedidaBaseId = unidad.Id,
            costoEstandar = 2m, bloqueado = 1, costoUnitario = 500m, costoAjustado = false
        });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var tras = (await put.Content.ReadFromJsonAsync<ProductoResponse>())!;
        Assert.Equal(categoria.Codigo, tras.CategoriaCodigo);
        Assert.Equal(unidad.Codigo, tras.UnidadMedidaCodigo);
        Assert.Equal(9.9m, tras.PrecioVenta);
        Assert.Equal(BloqueoProducto.Venta, tras.Bloqueado);
        Assert.Equal(0m, tras.CostoUnitario);
        Assert.True(tras.CostoAjustado);
    }

    [Fact]
    public async Task Create_ConStockEnElCuerpo_SeIgnora_YLaExistenciaNaceEn0()
    {
        // Task 3.6: la columna Stock ya no existe; System.Text.Json ignora en silencio la propiedad desconocida.
        var client = CreateClient("Administrador");

        var create = await client.PostAsJsonAsync("/api/productos", new { codigo = CodigoUnico("STK"), nombre = "Con stock ignorado", precioVenta = 1m, stock = 50 });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var creado = (await create.Content.ReadFromJsonAsync<ProductoResponse>())!;
        Assert.Equal(0m, creado.Existencia);

        var put = await client.PutAsJsonAsync($"/api/productos/{creado.Id}", new { codigo = creado.Codigo, nombre = "B", stock = 999 });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var tras = (await put.Content.ReadFromJsonAsync<ProductoResponse>())!;
        Assert.Equal(0m, tras.Existencia);
    }

    [Fact]
    public async Task List_FiltraPorExistenciaConSemanticaMayorOIgual_YPorStockState()
    {
        var client = CreateClient("Administrador");
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var conExistencia = (await (await client.PostAsJsonAsync("/api/productos", new { codigo = $"EX1-{sufijo}", nombre = $"Con existencia {sufijo}", precioVenta = 1m })).Content.ReadFromJsonAsync<ProductoResponse>())!;
        var sinExistencia = (await (await client.PostAsJsonAsync("/api/productos", new { codigo = $"EX0-{sufijo}", nombre = $"Sin existencia {sufijo}", precioVenta = 1m })).Content.ReadFromJsonAsync<ProductoResponse>())!;
        await RegistrarEntradaAsync(conExistencia.Id, 10m);

        // existencia=10 (>=): incluye al de existencia 10, no al de existencia 0.
        var porExistencia = (await client.GetFromJsonAsync<PagedResult<ProductoResponse>>($"/api/productos?codigo={sufijo}&existencia=10"))!;
        Assert.Contains(porExistencia.Items, p => p.Id == conExistencia.Id);
        Assert.DoesNotContain(porExistencia.Items, p => p.Id == sinExistencia.Id);

        // existencia=11 (>=): ya no incluye al de existencia 10.
        var porExistenciaAlta = (await client.GetFromJsonAsync<PagedResult<ProductoResponse>>($"/api/productos?codigo={sufijo}&existencia=11"))!;
        Assert.DoesNotContain(porExistenciaAlta.Items, p => p.Id == conExistencia.Id);

        // stockState=with / without / all, calculado sobre la existencia derivada.
        var conStock = (await client.GetFromJsonAsync<PagedResult<ProductoResponse>>($"/api/productos?codigo={sufijo}&stockState=with"))!;
        Assert.Contains(conStock.Items, p => p.Id == conExistencia.Id);
        Assert.DoesNotContain(conStock.Items, p => p.Id == sinExistencia.Id);

        var sinStock = (await client.GetFromJsonAsync<PagedResult<ProductoResponse>>($"/api/productos?codigo={sufijo}&stockState=without"))!;
        Assert.DoesNotContain(sinStock.Items, p => p.Id == conExistencia.Id);
        Assert.Contains(sinStock.Items, p => p.Id == sinExistencia.Id);

        var todos = (await client.GetFromJsonAsync<PagedResult<ProductoResponse>>($"/api/productos?codigo={sufijo}&stockState=all"))!;
        Assert.Equal(2, todos.Items.Count(p => p.Id == conExistencia.Id || p.Id == sinExistencia.Id));
    }

    [Fact]
    public async Task List_FiltraPorExistenciaConSemanticaMayorOIgual_ConCincoYDiez_DevuelveAmbos()
    {
        // Mata el mutante que cambia el comparador de "existencia" de >= a =: con existencia=5, un producto con
        // existencia 10 (mayor, no igual) debe seguir apareciendo. El test existente (10/11 contra 10/0) no lo
        // distingue porque en ambos casos el filtro con = habría coincidido igual.
        var client = CreateClient("Administrador");
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var conCinco = (await (await client.PostAsJsonAsync("/api/productos", new { codigo = $"EX5-{sufijo}", nombre = $"Existencia cinco {sufijo}", precioVenta = 1m })).Content.ReadFromJsonAsync<ProductoResponse>())!;
        var conDiez = (await (await client.PostAsJsonAsync("/api/productos", new { codigo = $"EX10-{sufijo}", nombre = $"Existencia diez {sufijo}", precioVenta = 1m })).Content.ReadFromJsonAsync<ProductoResponse>())!;
        await RegistrarEntradaAsync(conCinco.Id, 5m);
        await RegistrarEntradaAsync(conDiez.Id, 10m);

        var porExistencia = (await client.GetFromJsonAsync<PagedResult<ProductoResponse>>($"/api/productos?codigo={sufijo}&existencia=5"))!;

        Assert.Contains(porExistencia.Items, p => p.Id == conCinco.Id);
        Assert.Contains(porExistencia.Items, p => p.Id == conDiez.Id);
    }

    [Fact]
    public async Task GetExistencias_DevuelveSoloAlmacenesConMovimientos_Y404SiElProductoNoExiste()
    {
        var client = CreateClient("Administrador");
        var creado = (await (await client.PostAsJsonAsync("/api/productos", new { codigo = CodigoUnico("EXA"), nombre = "Con almacenes", precioVenta = 1m })).Content.ReadFromJsonAsync<ProductoResponse>())!;

        var sinMovimientos = await client.GetFromJsonAsync<List<ExistenciaAlmacen>>($"/api/productos/{creado.Id}/existencias");
        Assert.NotNull(sinMovimientos);
        Assert.Empty(sinMovimientos);

        await RegistrarEntradaAsync(creado.Id, 25m);

        var conMovimientos = await client.GetFromJsonAsync<List<ExistenciaAlmacen>>($"/api/productos/{creado.Id}/existencias");
        Assert.NotNull(conMovimientos);
        var almacen = Assert.Single(conMovimientos);
        Assert.Equal(AlmacenPrincipal, almacen.AlmacenId);
        Assert.Equal(25m, almacen.Existencia);

        var noExiste = await client.GetAsync($"/api/productos/{Guid.NewGuid()}/existencias");
        Assert.Equal(HttpStatusCode.NotFound, noExiste.StatusCode);
    }

    [Fact]
    public async Task Update_ConReferenciaInexistenteODatosInvalidos_Devuelve400_YSiElProductoNoExiste404()
    {
        var client = CreateClient("Administrador");
        var creado = (await (await client.PostAsJsonAsync("/api/productos", new { codigo = CodigoUnico("UPE"), nombre = "A" })).Content.ReadFromJsonAsync<ProductoResponse>())!;

        var categoriaMala = await client.PutAsJsonAsync($"/api/productos/{creado.Id}", new { codigo = creado.Codigo, nombre = "A", categoriaId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, categoriaMala.StatusCode);

        var unidadMala = await client.PutAsJsonAsync($"/api/productos/{creado.Id}", new { codigo = creado.Codigo, nombre = "A", unidadMedidaBaseId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, unidadMala.StatusCode);

        var bloqueoMalo = await client.PutAsJsonAsync($"/api/productos/{creado.Id}", new { codigo = creado.Codigo, nombre = "A", bloqueado = 9 });
        Assert.Equal(HttpStatusCode.BadRequest, bloqueoMalo.StatusCode);

        var sinNombre = await client.PutAsJsonAsync($"/api/productos/{creado.Id}", new { codigo = creado.Codigo, nombre = " " });
        Assert.Equal(HttpStatusCode.BadRequest, sinNombre.StatusCode);

        var noExiste = await client.PutAsJsonAsync($"/api/productos/{Guid.NewGuid()}", new { codigo = "X", nombre = "X" });
        Assert.Equal(HttpStatusCode.NotFound, noExiste.StatusCode);
    }

    [Fact]
    public async Task Update_CambiaUnidadBaseConMovimientos_Devuelve409_YNoModifica()
    {
        var client = CreateClient("Administrador");
        var unidad = await CrearUnidadAsync(client);
        var creado = (await (await client.PostAsJsonAsync("/api/productos", new { codigo = CodigoUnico("UMB"), nombre = "Con movimientos" })).Content.ReadFromJsonAsync<ProductoResponse>())!;
        await RegistrarEntradaAsync(creado.Id, 10m);

        var put = await client.PutAsJsonAsync($"/api/productos/{creado.Id}", new { codigo = creado.Codigo, nombre = creado.Nombre, unidadMedidaBaseId = unidad.Id });

        Assert.Equal(HttpStatusCode.Conflict, put.StatusCode);
        var cuerpo = await put.Content.ReadAsStringAsync();
        Assert.Contains("UnidadMedidaBaseId", cuerpo);
        Assert.Contains("unidad base no puede cambiarse", cuerpo, StringComparison.OrdinalIgnoreCase);

        // No se modificó nada: la unidad base sigue siendo la original (UND por defecto).
        var tras = (await client.GetFromJsonAsync<ProductoResponse>($"/api/productos/{creado.Id}"))!;
        Assert.Equal("UND", tras.UnidadMedidaCodigo);
        Assert.Equal(creado.Nombre, tras.Nombre);
    }

    [Fact]
    public async Task Update_CambiaUnidadBaseSinMovimientos_Devuelve200()
    {
        var client = CreateClient("Administrador");
        var unidad = await CrearUnidadAsync(client);
        var creado = (await (await client.PostAsJsonAsync("/api/productos", new { codigo = CodigoUnico("UMS"), nombre = "Sin movimientos" })).Content.ReadFromJsonAsync<ProductoResponse>())!;

        var put = await client.PutAsJsonAsync($"/api/productos/{creado.Id}", new { codigo = creado.Codigo, nombre = creado.Nombre, unidadMedidaBaseId = unidad.Id });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var tras = (await put.Content.ReadFromJsonAsync<ProductoResponse>())!;
        Assert.Equal(unidad.Id, tras.UnidadMedidaBaseId);
    }

    [Fact]
    public async Task Update_ConLaMismaUnidadBaseYMovimientos_Devuelve200()
    {
        // "Cambiar" a la misma unidad no es un cambio real: la guarda no debe activarse.
        var client = CreateClient("Administrador");
        var creado = (await (await client.PostAsJsonAsync("/api/productos", new { codigo = CodigoUnico("UMI"), nombre = "Misma unidad" })).Content.ReadFromJsonAsync<ProductoResponse>())!;
        await RegistrarEntradaAsync(creado.Id, 5m);

        var put = await client.PutAsJsonAsync($"/api/productos/{creado.Id}", new { codigo = creado.Codigo, nombre = "Renombrado", unidadMedidaBaseId = creado.UnidadMedidaBaseId });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var tras = (await put.Content.ReadFromJsonAsync<ProductoResponse>())!;
        Assert.Equal("Renombrado", tras.Nombre);
        Assert.Equal(creado.UnidadMedidaBaseId, tras.UnidadMedidaBaseId);
    }

    [Fact]
    public async Task List_FiltraYOrdenaPorTodasLasColumnasSobreElJoin_SinErrorDeColumnaAmbigua()
    {
        // Un JOIN con CategoriasProducto y UnidadesMedida deja "Codigo", "Nombre", "Id", "IsDeleted" y "CreatedAtUtc" en TRES tablas, y
        // ColumnasPermitidas.Citar no lleva alias de tabla: sin la subconsulta aplanada TODO filtro u orden daría 500 ("column reference
        // is ambiguous"). Solo se ve ejecutando la consulta contra Postgres.
        var client = CreateClient("Administrador");
        var sufijo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var categoria = await CrearCategoriaAsync(client, $"CAT{sufijo}", $"Categoria {sufijo}");
        var unidad = await CrearUnidadAsync(client, $"U{sufijo}", $"Unidad {sufijo}");
        var codigo = $"FIL-{sufijo}";
        var create = await client.PostAsJsonAsync("/api/productos", new
        {
            codigo, nombre = $"Nombre {sufijo}", precioVenta = 4321.5m, categoriaId = categoria.Id, unidadMedidaBaseId = unidad.Id
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<ProductoResponse>())!.Id;
        await RegistrarEntradaAsync(id, 4321m, unidadMedidaId: unidad.Id);

        var filtros = new (string Consulta, string Descripcion)[]
        {
            ($"codigo={Uri.EscapeDataString(codigo)}", "Codigo"),
            ($"nombre={Uri.EscapeDataString($"Nombre {sufijo}")}", "Nombre"),
            ($"categoriaCodigo={categoria.Codigo}", "CategoriaCodigo"),
            ($"categoriaNombre={Uri.EscapeDataString(categoria.Nombre)}", "CategoriaNombre"),
            ($"unidadMedidaCodigo={unidad.Codigo}", "UnidadMedidaCodigo"),
            ($"unidadMedidaNombre={Uri.EscapeDataString(unidad.Nombre)}", "UnidadMedidaNombre"),
            ("precioVenta=4321.5", "PrecioVenta"),
            ("existencia=4321", "Existencia"),
        };

        foreach (var (consulta, descripcion) in filtros)
        {
            var respuesta = await client.GetAsync($"/api/productos?{consulta}&tamanoPagina=50");
            Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"Filtro por {descripcion}: {(int)respuesta.StatusCode} {await respuesta.Content.ReadAsStringAsync()}");
            var pagina = (await respuesta.Content.ReadFromJsonAsync<PagedResult<ProductoResponse>>())!;
            Assert.True(pagina.Items.Any(p => p.Id == id), $"El filtro por {descripcion} no devolvió el producto");
            Assert.True(pagina.Items.All(p => p.Id != Guid.Empty));
        }

        // Todos los filtros a la vez.
        var todos = await client.GetAsync("/api/productos?" + string.Join("&", filtros.Select(f => f.Consulta)));
        Assert.Equal(HttpStatusCode.OK, todos.StatusCode);
        var unico = (await todos.Content.ReadFromJsonAsync<PagedResult<ProductoResponse>>())!;
        Assert.Equal(id, Assert.Single(unico.Items).Id);
        Assert.Equal(1, unico.Total);

        // Un filtro que no coincide no devuelve el producto.
        var ninguno = (await client.GetFromJsonAsync<PagedResult<ProductoResponse>>($"/api/productos?codigo={Uri.EscapeDataString(codigo)}&categoriaCodigo=NOEXISTE"))!;
        Assert.Empty(ninguno.Items);

        // Orden por cada columna ordenable, ascendente y descendente (con filtro y sin él).
        foreach (var columna in new[] { "Codigo", "Nombre", "CategoriaCodigo", "CategoriaNombre", "UnidadMedidaCodigo", "UnidadMedidaNombre", "PrecioVenta", "Existencia", "CreatedAtUtc" })
        {
            foreach (var descendente in new[] { true, false })
            {
                var respuesta = await client.GetAsync($"/api/productos?ordenarPor={columna}&descendente={descendente.ToString().ToLowerInvariant()}&tamanoPagina=50");
                Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"Orden por {columna} ({(descendente ? "desc" : "asc")}): {(int)respuesta.StatusCode} {await respuesta.Content.ReadAsStringAsync()}");

                var conFiltro = await client.GetAsync($"/api/productos?nombre={Uri.EscapeDataString($"Nombre {sufijo}")}&ordenarPor={columna}&descendente={descendente.ToString().ToLowerInvariant()}");
                Assert.Equal(HttpStatusCode.OK, conFiltro.StatusCode);
            }
        }

        // El orden se aplica de verdad sobre las columnas del JOIN: por CategoriaCodigo la nuestra queda entre las demás según su código.
        var porCategoria = (await client.GetFromJsonAsync<PagedResult<ProductoResponse>>("/api/productos?ordenarPor=CategoriaCodigo&descendente=false&tamanoPagina=100"))!;
        var codigos = porCategoria.Items.Select(p => p.CategoriaCodigo).ToList();
        Assert.Equal(codigos.Order(StringComparer.Ordinal).ToList(), codigos.ToList(), StringComparer.Ordinal);
    }

    [Fact]
    public async Task List_ElProductoSigueApareciendoSiSuCategoriaOSuUnidadFueronBorradasLogicamente()
    {
        // LEFT JOIN: la subconsulta no filtra IsDeleted de los catálogos. Los guardas de borrado impiden llegar a este estado por la API,
        // así que se fuerza en la BD (datos previos a los guardas, o una carrera).
        var client = CreateClient("Administrador");
        var categoria = await CrearCategoriaAsync(client);
        var unidad = await CrearUnidadAsync(client);
        var creado = (await (await client.PostAsJsonAsync("/api/productos", new
        {
            codigo = CodigoUnico("LFT"), nombre = "Con catalogos borrados", categoriaId = categoria.Id, unidadMedidaBaseId = unidad.Id
        })).Content.ReadFromJsonAsync<ProductoResponse>())!;
        await EjecutarAsync($"""UPDATE "CategoriasProducto" SET "IsDeleted" = true WHERE "Id" = '{categoria.Id}'; UPDATE "UnidadesMedida" SET "IsDeleted" = true WHERE "Id" = '{unidad.Id}';""");

        var lista = (await client.GetFromJsonAsync<PagedResult<ProductoResponse>>($"/api/productos?codigo={Uri.EscapeDataString(creado.Codigo)}"))!;
        var detalle = await client.GetAsync($"/api/productos/{creado.Id}");

        var item = Assert.Single(lista.Items);
        Assert.Equal(categoria.Codigo, item.CategoriaCodigo);
        Assert.Equal(unidad.Codigo, item.UnidadMedidaCodigo);
        Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);

        // Y sigue siendo editable sin tocar esas referencias (no se revalidan si no cambian).
        var put = await client.PutAsJsonAsync($"/api/productos/{creado.Id}", new { codigo = creado.Codigo, nombre = "Renombrado con catalogos borrados" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
    }

    [Fact]
    public async Task Guardas_DeUnidadYCategoriaEnUsoPorProductos_Devuelven409()
    {
        var client = CreateClient("Administrador");
        var categoria = await CrearCategoriaAsync(client);
        var unidad = await CrearUnidadAsync(client);
        var creado = (await (await client.PostAsJsonAsync("/api/productos", new
        {
            codigo = CodigoUnico("USO"), nombre = "En uso", categoriaId = categoria.Id, unidadMedidaBaseId = unidad.Id
        })).Content.ReadFromJsonAsync<ProductoResponse>())!;

        // Borrar la unidad base de un producto: 409 unidad_medida.en_uso.conflicto.
        var borrarUnidad = await client.DeleteAsync($"/api/unidades-medida/{unidad.Id}");
        Assert.Equal(HttpStatusCode.Conflict, borrarUnidad.StatusCode);
        var cuerpoUnidad = await borrarUnidad.Content.ReadAsStringAsync();
        Assert.Contains("productos", cuerpoUnidad);

        // Cambiar el Codigo de una unidad en uso: 409; cambiar solo el nombre: 200.
        var cambiarCodigo = await client.PutAsJsonAsync($"/api/unidades-medida/{unidad.Id}", new { codigo = $"X{Guid.NewGuid():N}"[..10], nombre = unidad.Nombre, decimales = unidad.Decimales });
        Assert.Equal(HttpStatusCode.Conflict, cambiarCodigo.StatusCode);
        var cambiarNombre = await client.PutAsJsonAsync($"/api/unidades-medida/{unidad.Id}", new { codigo = unidad.Codigo, nombre = "Renombrada", decimales = unidad.Decimales });
        Assert.Equal(HttpStatusCode.OK, cambiarNombre.StatusCode);

        // Borrar una categoría con productos: 409 categoria_producto.en_uso.conflicto.
        var borrarCategoria = await client.DeleteAsync($"/api/categorias-producto/{categoria.Id}");
        Assert.Equal(HttpStatusCode.Conflict, borrarCategoria.StatusCode);
        Assert.Contains("productos asociados", await borrarCategoria.Content.ReadAsStringAsync());

        // Al borrar el producto, ya no bloquean.
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/productos/{creado.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/categorias-producto/{categoria.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/unidades-medida/{unidad.Id}")).StatusCode);
    }

    private static string CodigoUnico(string prefijo) => $"{prefijo}-{Guid.NewGuid():N}"[..20];

    private static async Task<CategoriaProductoResponse> CrearCategoriaAsync(HttpClient client, string? codigo = null, string? nombre = null)
    {
        var respuesta = await client.PostAsJsonAsync("/api/categorias-producto", new { codigo = codigo ?? $"C{Guid.NewGuid():N}"[..12].ToUpperInvariant(), nombre = nombre ?? "Categoria de prueba" });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<CategoriaProductoResponse>())!;
    }

    private static async Task<UnidadMedidaResponse> CrearUnidadAsync(HttpClient client, string? codigo = null, string? nombre = null)
    {
        var respuesta = await client.PostAsJsonAsync("/api/unidades-medida", new { codigo = codigo ?? $"U{Guid.NewGuid():N}"[..10].ToUpperInvariant(), nombre = nombre ?? "Unidad de prueba", decimales = 2 });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<UnidadMedidaResponse>())!;
    }

    private async Task EjecutarAsync(string sql)
    {
        await using var contexto = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_fixture.AppConnectionString).Options);
        await contexto.Database.ExecuteSqlRawAsync(sql);
    }

    [Fact]
    public async Task Listado_Y_GetPorId_NoDevuelvenUnProductoBorradoLogicamente()
    {
        // Protege el predicado "IsDeleted" = false del SELECT base compartido de DapperProductoReadRepository
        // (ProductosAplanados: lo usan el listado, el recuento y el GET por Id).
        var client = CreateClient("Administrador");
        var categoria = await CrearCategoriaAsync(client);
        var unidad = await CrearUnidadAsync(client);
        var marca = Guid.NewGuid().ToString("N")[..10];

        var conservado = await CrearProductoAsync(client, $"{marca}A", categoria.Id, unidad.Id);
        var borrado = await CrearProductoAsync(client, $"{marca}B", categoria.Id, unidad.Id);
        Assert.Equal(2, (await client.GetFromJsonAsync<PagedResult<ProductoResponse>>($"/api/productos?codigo={marca}"))!.Total);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/productos/{borrado}")).StatusCode);

        var paged = (await client.GetFromJsonAsync<PagedResult<ProductoResponse>>($"/api/productos?codigo={marca}"))!;
        Assert.Equal(1, paged.Total);
        Assert.Equal(conservado, Assert.Single(paged.Items).Id);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/productos/{conservado}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/productos/{borrado}")).StatusCode);
    }

    private static async Task<Guid> CrearProductoAsync(HttpClient client, string codigo, Guid categoriaId, Guid unidadId)
    {
        var respuesta = await client.PostAsJsonAsync("/api/productos", new
        {
            codigo, nombre = "Producto borrado logico", precioVenta = 1m, stock = 0, categoriaId, unidadMedidaBaseId = unidadId,
        });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<ProductoResponse>())!.Id;
    }

    private HttpClient CreateClient(string role)
    {
        _client.DefaultRequestHeaders.Remove("X-Test-Anonymous");
        _client.DefaultRequestHeaders.Remove("X-Test-User");
        _client.DefaultRequestHeaders.Remove("X-Test-Roles");
        _client.DefaultRequestHeaders.Add("X-Test-User", role.ToLowerInvariant());
        _client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return _client;
    }
}
