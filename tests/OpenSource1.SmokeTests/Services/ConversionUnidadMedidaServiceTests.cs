using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.ValueObjects;
using OpenSource1.Infrastructure.Data;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Services;

/// <summary>
/// Prueba <see cref="IConversionUnidadMedidaService"/> contra Postgres real (la semilla del
/// catálogo, el índice único parcial y el CHECK viven en la migración, no en un mock).
/// REQUIERE DOCKER. Mismo armado que <c>GeneradorNumeroDocumentoTests</c>: DI de
/// <c>AddApplicationData</c> + migraciones reales, sin pasar por WebApplicationFactory.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ConversionUnidadMedidaServiceTests : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private readonly PostgresTestFixture _fixture;
    private ServiceProvider _provider = null!;

    public ConversionUnidadMedidaServiceTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _fixture.AppConnectionString,
                ["ConnectionStrings:IdentityConnection"] = _fixture.IdentityConnectionString,
                ["Database:ApplyMigrationsOnStartup"] = "false",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddApplicationData(configuration);
        _provider = services.BuildServiceProvider();

        await using var contexto = NuevoContexto();
        await contexto.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    [Fact]
    public async Task ObtenerConversion_ConFactorUno_ConvierteALaMismaCantidad()
    {
        var (producto, und) = await SembrarAsync("UND", "UND", 1m);

        var resultado = await ObtenerConversionAsync(producto, und);

        Assert.True(resultado.EsExito);
        Assert.Equal(1m, resultado.Valor.Factor);
        Assert.Equal(7m, resultado.Valor.ConvertirExacta(7m).Valor);
    }

    [Fact]
    public async Task ObtenerConversion_ConFactorMayorAUno_Multiplica()
    {
        // 1 CJA = 12 UND: 3 cajas son 36 unidades base.
        var (producto, cja) = await SembrarAsync("UND", "CJA", 12m);

        var resultado = await ObtenerConversionAsync(producto, cja);

        Assert.True(resultado.EsExito);
        Assert.Equal(12m, resultado.Valor.Factor);
        Assert.Equal(36m, resultado.Valor.ConvertirExacta(3m).Valor);
    }

    [Fact]
    public async Task ObtenerConversion_ProductoInexistenteConLaUnidadBaseDeOtroProducto_DevuelveProductoNoEncontrado()
    {
        // La unidad es la base de OTRO producto (existente): no debe confundirse con la identidad del inexistente.
        var (otroProducto, unidadBaseDeOtro) = await SembrarAsync("UND", "UND", 1m, filaAsociada: false);

        var resultado = await ObtenerConversionAsync(Guid.NewGuid(), unidadBaseDeOtro);

        Assert.NotEqual(Guid.Empty, otroProducto);
        Assert.True(resultado.EsFallo);
        Assert.Equal("conversion.producto_no_encontrado", resultado.Errores[0].Codigo);
    }

    [Fact]
    public async Task ObtenerConversion_UnidadBaseSinFilaAsociada_UsaFactorUnoYLosDecimalesDeLaBase()
    {
        // Base KG (3 decimales) sin fila en UnidadesMedidaProducto: identidad; 1.235 es exacta, 1.23456 no (no se redondea).
        var (producto, kg) = await SembrarAsync("KG", "KG", 1m, filaAsociada: false);

        var resultado = await ObtenerConversionAsync(producto, kg);

        Assert.True(resultado.EsExito);
        Assert.Equal((1m, (short)3, "KG"), (resultado.Valor.Factor, resultado.Valor.DecimalesBase, resultado.Valor.CodigoUnidadBase));
        Assert.Equal(1.235m, resultado.Valor.ConvertirExacta(1.235m).Valor);
        Assert.Equal("conversion.cantidad_no_exacta", resultado.Valor.ConvertirExacta(1.23456m).Errores[0].Codigo);
    }

    [Fact]
    public async Task ObtenerConversion_UnidadBaseConFilaDeOtroFactor_GanaLaIdentidad()
    {
        // La base UND tiene por error una fila con factor 5: el servicio ignora la fila y usa factor 1.
        var (producto, und) = await SembrarAsync("UND", "UND", 5m);

        var resultado = await ObtenerConversionAsync(producto, und);

        Assert.True(resultado.EsExito);
        Assert.Equal(1m, resultado.Valor.Factor);
        Assert.Equal(7m, resultado.Valor.ConvertirExacta(7m).Valor);
    }

    [Fact]
    public async Task ObtenerConversion_UnidadNoBaseSinFila_DevuelveUnidadNoAsociada()
    {
        var (producto, _) = await SembrarAsync("UND", "UND", 1m, filaAsociada: false);
        var cja = await IdUnidadAsync("CJA");

        var resultado = await ObtenerConversionAsync(producto, cja);

        Assert.True(resultado.EsFallo);
        Assert.Equal(("conversion.unidad_no_asociada", "UnidadMedidaId"), (resultado.Errores[0].Codigo, resultado.Errores[0].Campo));
    }

    [Fact]
    public async Task ObtenerConversion_ProductoBorradoLogicamente_DevuelveProductoNoEncontrado()
    {
        var (producto, cja) = await SembrarAsync("UND", "CJA", 12m);
        await using (var contexto = NuevoContexto())
        {
            (await contexto.Productos.SingleAsync(p => p.Id == producto)).IsDeleted = true;
            await contexto.SaveChangesAsync();
        }

        var resultado = await ObtenerConversionAsync(producto, cja);

        Assert.True(resultado.EsFallo);
        Assert.Equal("conversion.producto_no_encontrado", resultado.Errores[0].Codigo);
    }

    [Fact]
    public async Task ObtenerConversion_UnidadBaseBorradaDelCatalogo_DevuelveUnidadBaseNoEncontrada()
    {
        var (producto, cja) = await SembrarAsync("PAQ", "CJA", 4m);

        // Borrado lógico simulado a mano (IsDeleted = true): un DbContext crudo no pasa por el
        // borrado lógico del UnitOfWork. Se restaura al final para no alterar el catálogo
        // sembrado que verifican otras pruebas de esta clase.
        await CambiarBorradoPaqAsync(true);
        Result<ConversionUnidadMedida> resultado;
        try
        {
            resultado = await ObtenerConversionAsync(producto, cja);
        }
        finally
        {
            await CambiarBorradoPaqAsync(false);
        }

        Assert.True(resultado.EsFallo);
        Assert.Equal("conversion.unidad_base_no_encontrada", resultado.Errores[0].Codigo);
    }

    [Fact]
    public async Task ObtenerConversion_UsaLosDecimalesDeLaUnidadBase_NoLosDeLaDeEntrada()
    {
        // Base UND (0 decimales), entrada KG (3 decimales): 1 KG = 2.5 UND. Con los decimales de la entrada 2.5 sería
        // válida; con los de la base (correcto) se rechaza, y 2 KG = 5 UND es exacta.
        var (productoUnd, kg) = await SembrarAsync("UND", "KG", 2.5m);
        var haciaBaseSinDecimales = (await ObtenerConversionAsync(productoUnd, kg)).Valor;
        Assert.Equal(((short)0, "UND"), (haciaBaseSinDecimales.DecimalesBase, haciaBaseSinDecimales.CodigoUnidadBase));
        Assert.Equal("conversion.cantidad_no_exacta", haciaBaseSinDecimales.ConvertirExacta(1m).Errores[0].Codigo);
        Assert.Equal(5m, haciaBaseSinDecimales.ConvertirExacta(2m).Valor);

        // Base KG (3 decimales), entrada CJA (0 decimales): 1 CJA = 0.25 KG. Con los decimales de la entrada no cabría;
        // con los de la base (correcto) es 0.25.
        var (productoKg, cja) = await SembrarAsync("KG", "CJA", 0.25m);
        var haciaBaseConDecimales = (await ObtenerConversionAsync(productoKg, cja)).Valor;
        Assert.Equal((short)3, haciaBaseConDecimales.DecimalesBase);
        Assert.Equal(0.25m, haciaBaseConDecimales.ConvertirExacta(1m).Valor);
    }

    [Fact]
    public async Task ElCatalogoSemilla_ContieneLasDiezUnidadesIniciales_ConNombreDecimalesYNoBorradas()
    {
        await using var contexto = NuevoContexto();
        // IgnoreQueryFilters para poder comprobar la columna IsDeleted de las filas sembradas.
        var filas = await contexto.UnidadesMedida.IgnoreQueryFilters().ToListAsync();

        foreach (var (codigo, nombre, decimales) in UnidadesMedidaSemilla.Catalogo)
        {
            var fila = Assert.Single(filas, u => u.Codigo == codigo);
            Assert.Equal(nombre, fila.Nombre);
            Assert.Equal(decimales, fila.Decimales);
            Assert.False(fila.IsDeleted);
        }
    }

    [Fact]
    public async Task UnidadMedidaProducto_RechazaCantidadNoPositiva_ConElCheckDeLaBaseDeDatos()
    {
        var (producto, unidad) = await SembrarAsync("UND", "CJA", 12m);
        await using var contexto = NuevoContexto();
        contexto.UnidadesMedidaProducto.Add(new UnidadMedidaProducto
        {
            ProductoId = producto,
            UnidadMedidaId = await IdUnidadAsync("DOC"),
            CantidadPorUnidadMedida = 0m,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => contexto.SaveChangesAsync());
        Assert.NotEqual(Guid.Empty, unidad);
    }

    [Fact]
    public async Task UnidadMedidaProducto_RechazaDuplicadoActivo_PeroPermiteReasociarTrasBorrar()
    {
        var (producto, unidad) = await SembrarAsync("UND", "CJA", 12m);

        await using (var duplicado = NuevoContexto())
        {
            duplicado.UnidadesMedidaProducto.Add(new UnidadMedidaProducto
            {
                ProductoId = producto, UnidadMedidaId = unidad, CantidadPorUnidadMedida = 6m,
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicado.SaveChangesAsync());
        }

        await using (var borrado = NuevoContexto())
        {
            var existente = await borrado.UnidadesMedidaProducto
                .SingleAsync(x => x.ProductoId == producto && x.UnidadMedidaId == unidad);
            existente.IsDeleted = true;
            await borrado.SaveChangesAsync();
        }

        await using var reasociado = NuevoContexto();
        reasociado.UnidadesMedidaProducto.Add(new UnidadMedidaProducto
        {
            ProductoId = producto, UnidadMedidaId = unidad, CantidadPorUnidadMedida = 6m,
        });
        await reasociado.SaveChangesAsync();
    }

    [Fact]
    public async Task ObtenerConversion_FactorNoEntero_BaseConDosDecimales_AceptaSoloCantidadesExactasEnLaBase()
    {
        // Caja de 3 metros (base MT, 2 decimales): 0.5 CJA = 1.5 MT es exacta; 1/3 CJA (0.333333) = 0.999999 MT no lo es.
        var (producto, cja) = await SembrarAsync("MT", "CJA", 3m);

        var resultado = await ObtenerConversionAsync(producto, cja);

        Assert.True(resultado.EsExito);
        var conversion = resultado.Valor;
        Assert.Equal((3m, (short)2, "MT"), (conversion.Factor, conversion.DecimalesBase, conversion.CodigoUnidadBase));
        Assert.Equal(1.5m, conversion.ConvertirExacta(0.5m).Valor);
        var tercio = conversion.ConvertirExacta(0.333333m);
        Assert.True(tercio.EsFallo);
        Assert.Equal(("conversion.cantidad_no_exacta", "Cantidad"), (tercio.Errores[0].Codigo, tercio.Errores[0].Campo));
        Assert.Contains("0.999999 MT", tercio.Errores[0].Mensaje);
        Assert.Contains("como máximo 2 decimales", tercio.Errores[0].Mensaje);
    }

    [Fact]
    public async Task ObtenerConversion_UnidadBaseSinDecimales_RechazaDosYMedio_NoRedondea()
    {
        var (producto, und) = await SembrarAsync("UND", "UND", 1m, filaAsociada: false);

        var conversion = (await ObtenerConversionAsync(producto, und)).Valor;

        Assert.Equal(3m, conversion.ConvertirExacta(3m).Valor);
        var error = Assert.Single(conversion.ConvertirExacta(2.5m).Errores);
        Assert.Equal("conversion.cantidad_no_exacta", error.Codigo);
        Assert.Contains("no admite decimales", error.Mensaje);
    }

    [Fact]
    public async Task ObtenerConversion_UnidadNoAsociadaOProductoInexistente_DevuelveSusCodigos()
    {
        var (producto, _) = await SembrarAsync("UND", "CJA", 12m);

        var noAsociada = await ObtenerConversionAsync(producto, await IdUnidadAsync("KG"));
        var inexistente = await ObtenerConversionAsync(Guid.NewGuid(), await IdUnidadAsync("UND"));

        Assert.Equal("conversion.unidad_no_asociada", noAsociada.Errores[0].Codigo);
        Assert.Equal("conversion.producto_no_encontrado", inexistente.Errores[0].Codigo);
    }

    [Fact]
    public void ConvertirExacta_CantidadBaseFueraDeNumeric18_6_EsCantidadInvalida()
    {
        var conversion = new ConversionUnidadMedida(1_000_000m, 0, "UND");

        var error = Assert.Single(conversion.ConvertirExacta(999_999_999m).Errores);

        Assert.Equal(("conversion.cantidad_invalida", "Cantidad"), (error.Codigo, error.Campo));
    }

    private async Task<Result<ConversionUnidadMedida>> ObtenerConversionAsync(Guid productoId, Guid unidadId)
    {
        await using var scope = _provider.CreateAsyncScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IConversionUnidadMedidaService>();
        return await servicio.ObtenerConversionAsync(productoId, unidadId);
    }

    private async Task CambiarBorradoPaqAsync(bool borrado)
    {
        await using var contexto = NuevoContexto();
        var paq = await contexto.UnidadesMedida.IgnoreQueryFilters().SingleAsync(u => u.Codigo == "PAQ");
        paq.IsDeleted = borrado;
        await contexto.SaveChangesAsync();
    }

    private ApplicationDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_fixture.AppConnectionString).Options);

    private async Task<Guid> IdUnidadAsync(string codigo)
    {
        await using var contexto = NuevoContexto();
        return await contexto.UnidadesMedida.Where(u => u.Codigo == codigo).Select(u => u.Id).SingleAsync();
    }

    /// <summary>
    /// Crea un producto con la unidad base indicada y (salvo <paramref name="filaAsociada"/> = false) le asocia la
    /// unidad/factor dados. Devuelve siempre el id de la unidad <paramref name="codigoUnidad"/>.
    /// </summary>
    private async Task<(Guid ProductoId, Guid UnidadMedidaId)> SembrarAsync(
        string codigoBase, string codigoUnidad, decimal factor, bool filaAsociada = true)
    {
        await using var contexto = NuevoContexto();
        var categoriaId = await contexto.CategoriasProducto.Where(c => c.Codigo == "GENERAL").Select(c => c.Id).SingleAsync();
        var unidadBaseId = await contexto.UnidadesMedida.Where(u => u.Codigo == codigoBase).Select(u => u.Id).SingleAsync();
        var producto = new Producto
        {
            Codigo = $"P{Guid.NewGuid():N}"[..20],
            Nombre = "Producto de prueba de conversión",
            PrecioVenta = 1m,
            CategoriaId = categoriaId,
            UnidadMedidaBaseId = unidadBaseId,
        };
        contexto.Productos.Add(producto);

        var unidadId = await contexto.UnidadesMedida.Where(u => u.Codigo == codigoUnidad).Select(u => u.Id).SingleAsync();
        if (filaAsociada)
        {
            contexto.UnidadesMedidaProducto.Add(new UnidadMedidaProducto
            {
                ProductoId = producto.Id,
                UnidadMedidaId = unidadId,
                CantidadPorUnidadMedida = factor,
            });
        }

        await contexto.SaveChangesAsync();

        return (producto.Id, unidadId);
    }
}
