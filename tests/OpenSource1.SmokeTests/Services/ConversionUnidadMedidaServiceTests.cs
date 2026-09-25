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
    public async Task ConvertirABase_ConFactorUno_DevuelveLaMismaCantidad()
    {
        var (producto, und) = await SembrarAsync("UND", "UND", 1m);

        var resultado = await ConvertirAsync(producto, und, 7m);

        Assert.True(resultado.EsExito);
        Assert.Equal(7m, resultado.Valor);
    }

    [Fact]
    public async Task ConvertirABase_ConFactorMayorAUno_Multiplica()
    {
        // 1 CJA = 12 UND: 3 cajas son 36 unidades base.
        var (producto, cja) = await SembrarAsync("UND", "CJA", 12m);

        var resultado = await ConvertirAsync(producto, cja, 3m);

        Assert.True(resultado.EsExito);
        Assert.Equal(36m, resultado.Valor);
    }

    [Fact]
    public async Task ConvertirABase_UnidadNoAsociadaAlProducto_DevuelveFallo()
    {
        var (producto, _) = await SembrarAsync("UND", "CJA", 12m);
        var kg = await IdUnidadAsync("KG");

        var resultado = await ConvertirAsync(producto, kg, 1m);

        Assert.True(resultado.EsFallo);
        Assert.Equal("conversion.unidad_no_asociada", resultado.Errores[0].Codigo);
    }

    [Fact]
    public async Task ConvertirABase_ProductoInexistente_DevuelveProductoNoEncontrado()
    {
        var resultado = await ConvertirAsync(Guid.NewGuid(), await IdUnidadAsync("UND"), 1m);

        Assert.True(resultado.EsFallo);
        Assert.Equal("conversion.producto_no_encontrado", resultado.Errores[0].Codigo);
    }

    [Fact]
    public async Task ConvertirABase_ProductoInexistenteConLaUnidadBaseDeOtroProducto_DevuelveProductoNoEncontrado()
    {
        // La unidad es la base de OTRO producto (existente): no debe confundirse con la identidad del inexistente.
        var (otroProducto, unidadBaseDeOtro) = await SembrarAsync("UND", "UND", 1m, filaAsociada: false);

        var resultado = await ConvertirAsync(Guid.NewGuid(), unidadBaseDeOtro, 1m);

        Assert.NotEqual(Guid.Empty, otroProducto);
        Assert.True(resultado.EsFallo);
        Assert.Equal("conversion.producto_no_encontrado", resultado.Errores[0].Codigo);
    }

    [Fact]
    public async Task ConvertirABase_UnidadBaseSinFilaAsociada_UsaFactorUnoYRedondeaConSusDecimales()
    {
        // Base KG (3 decimales) sin fila en UnidadesMedidaProducto: identidad, 1.23456 -> 1.235 (away from zero).
        var (producto, kg) = await SembrarAsync("KG", "KG", 1m, filaAsociada: false);

        var resultado = await ConvertirAsync(producto, kg, 1.23456m);

        Assert.True(resultado.EsExito);
        Assert.Equal(1.235m, resultado.Valor);
    }

    [Fact]
    public async Task ConvertirABase_UnidadBaseConFilaDeOtroFactor_GanaLaIdentidad()
    {
        // La base UND tiene por error una fila con factor 5: el servicio ignora la fila y usa factor 1.
        var (producto, und) = await SembrarAsync("UND", "UND", 5m);

        var resultado = await ConvertirAsync(producto, und, 7m);

        Assert.True(resultado.EsExito);
        Assert.Equal(7m, resultado.Valor);
    }

    [Fact]
    public async Task ConvertirABase_UnidadNoBaseSinFila_DevuelveUnidadNoAsociada()
    {
        var (producto, _) = await SembrarAsync("UND", "UND", 1m, filaAsociada: false);
        var cja = await IdUnidadAsync("CJA");

        var resultado = await ConvertirAsync(producto, cja, 1m);

        Assert.True(resultado.EsFallo);
        Assert.Equal("conversion.unidad_no_asociada", resultado.Errores[0].Codigo);
    }

    [Fact]
    public async Task ConvertirABase_ProductoBorradoLogicamente_DevuelveProductoNoEncontrado()
    {
        var (producto, cja) = await SembrarAsync("UND", "CJA", 12m);
        await using (var contexto = NuevoContexto())
        {
            (await contexto.Productos.SingleAsync(p => p.Id == producto)).IsDeleted = true;
            await contexto.SaveChangesAsync();
        }

        var resultado = await ConvertirAsync(producto, cja, 1m);

        Assert.True(resultado.EsFallo);
        Assert.Equal("conversion.producto_no_encontrado", resultado.Errores[0].Codigo);
    }

    [Fact]
    public async Task ConvertirABase_UnidadBaseBorradaDelCatalogo_DevuelveUnidadBaseNoEncontrada()
    {
        var (producto, cja) = await SembrarAsync("PAQ", "CJA", 4m);

        // Borrado lógico simulado a mano (IsDeleted = true): un DbContext crudo no pasa por el
        // borrado lógico del UnitOfWork. Se restaura al final para no alterar el catálogo
        // sembrado que verifican otras pruebas de esta clase.
        await CambiarBorradoPaqAsync(true);
        Result<decimal> resultado;
        try
        {
            resultado = await ConvertirAsync(producto, cja, 1m);
        }
        finally
        {
            await CambiarBorradoPaqAsync(false);
        }

        Assert.True(resultado.EsFallo);
        Assert.Equal("conversion.unidad_base_no_encontrada", resultado.Errores[0].Codigo);
    }

    [Fact]
    public async Task ConvertirABase_RedondeaConLosDecimalesDeLaUnidadBase_NoDeLaDeEntrada()
    {
        // Base UND (0 decimales), entrada KG (3 decimales): 1 KG = 2.5 UND. Con los decimales de
        // la unidad de entrada quedaría 2.5; con los de la base (correcto) es 3 (away from zero).
        var (productoUnd, kg) = await SembrarAsync("UND", "KG", 2.5m);
        var haciaBaseSinDecimales = await ConvertirAsync(productoUnd, kg, 1m);
        Assert.True(haciaBaseSinDecimales.EsExito);
        Assert.Equal(3m, haciaBaseSinDecimales.Valor);

        // Base KG (3 decimales), entrada CJA (0 decimales): 1 CJA = 0.333333 KG. Con los decimales
        // de la entrada daría 0; con los de la base (correcto) es 0.333.
        var (productoKg, cja) = await SembrarAsync("KG", "CJA", 0.333333m);
        var haciaBaseConDecimales = await ConvertirAsync(productoKg, cja, 1m);
        Assert.True(haciaBaseConDecimales.EsExito);
        Assert.Equal(0.333m, haciaBaseConDecimales.Valor);
    }

    [Fact]
    public async Task ConvertirABase_RedondeaMitadesAlejandoseDeCero()
    {
        // Base GR (0 decimales): 0.5 GR-por-unidad * 1 = 0.5 -> 1 ; 1.5 -> 2 (no banker's rounding).
        var (producto, mt) = await SembrarAsync("GR", "MT", 0.5m);
        Assert.Equal(1m, (await ConvertirAsync(producto, mt, 1m)).Valor);
        Assert.Equal(2m, (await ConvertirAsync(producto, mt, 3m)).Valor);
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

    private async Task CambiarBorradoPaqAsync(bool borrado)
    {
        await using var contexto = NuevoContexto();
        var paq = await contexto.UnidadesMedida.IgnoreQueryFilters().SingleAsync(u => u.Codigo == "PAQ");
        paq.IsDeleted = borrado;
        await contexto.SaveChangesAsync();
    }

    private async Task<Result<decimal>> ConvertirAsync(Guid productoId, Guid unidadId, decimal cantidad)
    {
        await using var scope = _provider.CreateAsyncScope();
        var servicio = scope.ServiceProvider.GetRequiredService<IConversionUnidadMedidaService>();
        return await servicio.ConvertirABaseAsync(productoId, unidadId, cantidad);
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
            Stock = 0,
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
