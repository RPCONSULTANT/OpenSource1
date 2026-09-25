using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenSource1.Application.Data;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Enums;
using OpenSource1.Infrastructure.Data;

namespace OpenSource1.SmokeTests.TestInfrastructure;

/// <summary>
/// Armado compartido por los tests del servicio de registro/consulta del libro de inventario (Task 3.4): DI real de
/// <c>AddApplicationData</c> (sin WebApplicationFactory, mismo motivo que <c>GeneradorNumeroDocumentoTests</c>),
/// migraciones reales y siembra de productos/almacenes propios de cada test (datos aislados por Guid, sin limpieza).
/// </summary>
internal sealed class LibroInventarioPrueba(PostgresTestFixture fixture) : IAsyncDisposable
{
    public static readonly Guid UnidadUnd = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid CategoriaGeneral = Guid.Parse("c1000000-0000-0000-0000-000000000001");

    public ServiceProvider Provider { get; private set; } = null!;
    public Guid UnidadCja { get; private set; }

    public async Task InicializarAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = fixture.AppConnectionString,
                ["ConnectionStrings:IdentityConnection"] = fixture.IdentityConnectionString,
                ["Database:ApplyMigrationsOnStartup"] = "false",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddApplicationData(configuration);
        Provider = services.BuildServiceProvider();

        await using var contexto = NuevoContexto();
        await contexto.Database.MigrateAsync();
        UnidadCja = await contexto.UnidadesMedida.Where(u => u.Codigo == "CJA").Select(u => u.Id).SingleAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (Provider is not null)
        {
            await Provider.DisposeAsync();
        }
    }

    public ApplicationDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.AppConnectionString).Options);

    public NpgsqlConnection NuevaConexion() => new(fixture.AppConnectionString);

    /// <summary>Producto con base UND y la caja (CJA) asociada con factor 12.</summary>
    public async Task<Guid> SembrarProductoAsync(
        decimal costoUnitario = 0m, BloqueoProducto bloqueado = BloqueoProducto.Ninguno, bool borrado = false)
    {
        await using var contexto = NuevoContexto();
        var producto = new Producto
        {
            Codigo = $"INV{Guid.NewGuid():N}"[..20],
            Nombre = "Producto de prueba del libro de inventario",
            PrecioVenta = 1m,
            Stock = 0,
            CategoriaId = CategoriaGeneral,
            UnidadMedidaBaseId = UnidadUnd,
            CostoUnitario = costoUnitario,
            Bloqueado = bloqueado,
            IsDeleted = borrado,
            CreatedBy = "test",
        };
        contexto.Productos.Add(producto);
        contexto.UnidadesMedidaProducto.Add(new UnidadMedidaProducto
        {
            ProductoId = producto.Id,
            UnidadMedidaId = UnidadCja,
            CantidadPorUnidadMedida = 12m,
            CreatedBy = "test",
        });
        await contexto.SaveChangesAsync();
        return producto.Id;
    }

    public async Task<Guid> SembrarAlmacenAsync(bool bloqueado = false)
    {
        await using var contexto = NuevoContexto();
        var almacen = new Almacen
        {
            Codigo = $"A{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            Nombre = "Almacén de prueba",
            Bloqueado = bloqueado,
            EsPredeterminado = false,
            CreatedBy = "test",
        };
        contexto.Almacenes.Add(almacen);
        await contexto.SaveChangesAsync();
        return almacen.Id;
    }

    public static MovimientoInventarioSolicitud Entrada(
        Guid productoId, Guid almacenId, decimal cantidad, decimal? costo, DateOnly fecha,
        Guid? unidadId = null, TipoMovimientoInventario tipo = TipoMovimientoInventario.AjustePositivo) =>
        new(productoId, almacenId, tipo, cantidad, EsEntrada: true, unidadId ?? UnidadUnd, costo,
            fecha, fecha, TipoDocumentoInventario.RegistroDiario, "DOC-1", 1, TipoOrigenMovimiento.Diario,
            $"E-{Guid.NewGuid():N}");

    public static MovimientoInventarioSolicitud Salida(
        Guid productoId, Guid almacenId, decimal cantidad, DateOnly fecha, Guid? unidadId = null,
        TipoMovimientoInventario tipo = TipoMovimientoInventario.AjusteNegativo) =>
        new(productoId, almacenId, tipo, cantidad, EsEntrada: false, unidadId ?? UnidadUnd, null,
            fecha, fecha, TipoDocumentoInventario.RegistroDiario, "DOC-2", 1, TipoOrigenMovimiento.Diario,
            $"S-{Guid.NewGuid():N}");

    /// <summary>Registra en su propio scope y transacción; confirma si tuvo éxito, deshace si falló.</summary>
    public async Task<Result<MovimientoRegistrado>> RegistrarAsync(MovimientoInventarioSolicitud solicitud)
    {
        await using var scope = Provider.CreateAsyncScope();
        var sesion = scope.ServiceProvider.GetRequiredService<IDbSession>();
        var registro = scope.ServiceProvider.GetRequiredService<IRegistroMovimientosInventario>();

        await using var tx = await sesion.BeginTransactionAsync();
        var resultado = await registro.RegistrarAsync(solicitud);
        if (resultado.EsExito)
        {
            await sesion.CommitAsync();
        }
        else
        {
            await sesion.RollbackAsync();
        }

        return resultado;
    }

    /// <summary>Como <see cref="RegistrarAsync"/> pero exige éxito (mensaje legible si falla).</summary>
    public async Task<MovimientoRegistrado> RegistrarOkAsync(MovimientoInventarioSolicitud solicitud)
    {
        var resultado = await RegistrarAsync(solicitud);
        Assert.True(resultado.EsExito, resultado.EsFallo
            ? string.Join("; ", resultado.Errores.Select(e => $"{e.Codigo}: {e.Mensaje}"))
            : string.Empty);
        return resultado.Valor;
    }

    public async Task<T> ConsultarAsync<T>(Func<IConsultaInventario, Task<T>> accion)
    {
        await using var scope = Provider.CreateAsyncScope();
        var consulta = scope.ServiceProvider.GetRequiredService<IConsultaInventario>();
        return await accion(consulta);
    }

    /// <summary>Filas del libro de un producto: (MovimientosProducto, MovimientosValor, Aplicaciones).</summary>
    public static async Task<(long Producto, long Valor, long Aplicaciones)> ContarFilasAsync(
        System.Data.Common.DbConnection conexion, Guid productoId, System.Data.Common.DbTransaction? tx = null)
    {
        const string sql = """
            SELECT
              (SELECT COUNT(*) FROM "MovimientosProducto" WHERE "ProductoId" = @p),
              (SELECT COUNT(*) FROM "MovimientosValor" WHERE "ProductoId" = @p),
              (SELECT COUNT(*) FROM "AplicacionesMovimientoProducto" a
                 JOIN "MovimientosProducto" m ON m."Id" = a."MovimientoSalidaId" WHERE m."ProductoId" = @p)
            """;
        return await conexion.QuerySingleAsync<(long, long, long)>(sql, new { p = productoId }, tx);
    }

    public async Task<(long Producto, long Valor, long Aplicaciones)> ContarFilasAsync(Guid productoId)
    {
        await using var conexion = NuevaConexion();
        return await ContarFilasAsync(conexion, productoId);
    }
}
