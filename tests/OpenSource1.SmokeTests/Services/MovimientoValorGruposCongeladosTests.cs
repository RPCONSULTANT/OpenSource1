using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Services;

/// <summary>
/// Congelación de grupos en el libro de valor (Task 5.5, desviación D8 de la Fase 5): <c>RegistrarAsync</c> copia
/// <c>GrupoInventarioId</c>/<c>GrupoProductoId</c> del producto y <c>GrupoNegocioId</c> del socio al <c>MovimientoValor</c>;
/// cambiar después los grupos del maestro no cambia los movimientos ya escritos; un producto sin grupos sigue registrando
/// (congela null). Las columnas tienen FK a los catálogos de grupos. REQUIERE DOCKER.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class MovimientoValorGruposCongeladosTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>, IAsyncLifetime
{
    private static readonly DateOnly Fecha = new(2026, 9, 20);
    private readonly LibroInventarioPrueba _prueba = new(fixture);

    public Task InitializeAsync() => _prueba.InicializarAsync();

    public async Task DisposeAsync() => await _prueba.DisposeAsync();

    [Fact]
    public async Task Registrar_CongelaLosGruposDelProductoYDelSocio_YNoCambianAlCambiarElMaestro()
    {
        var almacen = await _prueba.SembrarAlmacenAsync();
        var producto = await _prueba.SembrarProductoAsync(costoUnitario: 2m);
        await AsignarGruposProductoAsync(producto, GrupoContableIds.ProductoBienes, GrupoContableIds.InventarioGeneral);
        var socio = await SembrarSocioAsync(GrupoContableIds.NegocioExterior);

        var entrada = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 10m, 2m, Fecha));
        var venta = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 3m, Fecha) with { SocioNegocioId = socio });

        Assert.Equal((GrupoContableIds.InventarioGeneral, GrupoContableIds.ProductoBienes, (Guid?)null), await GruposAsync(entrada.MovimientoValorId));
        Assert.Equal((GrupoContableIds.InventarioGeneral, GrupoContableIds.ProductoBienes, GrupoContableIds.NegocioExterior), await GruposAsync(venta.MovimientoValorId));

        // Congelados: reclasificar el producto y el socio después no toca los movimientos ya escritos; los nuevos sí toman
        // los grupos vigentes.
        var otroInventario = await GrupoInventarioAsync();
        await AsignarGruposProductoAsync(producto, GrupoContableIds.ProductoServicios, otroInventario);
        await using (var contexto = _prueba.NuevoContexto())
        {
            var s = await contexto.SociosNegocio.SingleAsync(x => x.Id == socio);
            s.GrupoNegocioId = GrupoContableIds.NegocioNacional;
            await contexto.SaveChangesAsync();
        }

        Assert.Equal((GrupoContableIds.InventarioGeneral, GrupoContableIds.ProductoBienes, GrupoContableIds.NegocioExterior), await GruposAsync(venta.MovimientoValorId));

        var otraVenta = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Salida(producto, almacen, 1m, Fecha) with { SocioNegocioId = socio });
        Assert.Equal((otroInventario, GrupoContableIds.ProductoServicios, GrupoContableIds.NegocioNacional), await GruposAsync(otraVenta.MovimientoValorId));
    }

    [Fact]
    public async Task Registrar_ProductoSinGruposYSocioSinGrupo_SigueRegistrando_YCongelaNull()
    {
        var almacen = await _prueba.SembrarAlmacenAsync();
        var producto = await _prueba.SembrarProductoAsync(costoUnitario: 1m);
        var socio = await SembrarSocioAsync(null);

        var entrada = await _prueba.RegistrarOkAsync(LibroInventarioPrueba.Entrada(producto, almacen, 5m, 1m, Fecha) with { SocioNegocioId = socio });

        Assert.Equal(((Guid?)null, (Guid?)null, (Guid?)null), await GruposAsync(entrada.MovimientoValorId));
    }

    [Fact]
    public async Task LasColumnasDeGruposDelLibroDeValor_TienenFkASusCatalogos()
    {
        await using var conexion = _prueba.NuevaConexion();
        var fks = (await conexion.QueryAsync<string>(
            """
            SELECT conname FROM pg_constraint
            WHERE conrelid = '"MovimientosValor"'::regclass AND contype = 'f' AND conname LIKE 'FK_MovimientosValor_Grupos%'
            ORDER BY conname
            """)).ToList();

        Assert.Equal(
            [
                "FK_MovimientosValor_GruposInventario_GrupoInventarioId",
                "FK_MovimientosValor_GruposNegocio_GrupoNegocioId",
                "FK_MovimientosValor_GruposProducto_GrupoProductoId",
            ],
            fks);
    }

    private async Task<(Guid? Inventario, Guid? Producto, Guid? Negocio)> GruposAsync(long movimientoValorId)
    {
        await using var conexion = _prueba.NuevaConexion();
        return await conexion.QuerySingleAsync<(Guid?, Guid?, Guid?)>(
            """SELECT "GrupoInventarioId", "GrupoProductoId", "GrupoNegocioId" FROM "MovimientosValor" WHERE "Id" = @Id""",
            new { Id = movimientoValorId });
    }

    private async Task AsignarGruposProductoAsync(Guid productoId, Guid grupoProducto, Guid grupoInventario)
    {
        await using var contexto = _prueba.NuevoContexto();
        var producto = await contexto.Productos.SingleAsync(x => x.Id == productoId);
        producto.GrupoProductoId = grupoProducto;
        producto.GrupoInventarioId = grupoInventario;
        await contexto.SaveChangesAsync();
    }

    private async Task<Guid> GrupoInventarioAsync()
    {
        await using var contexto = _prueba.NuevoContexto();
        var grupo = new GrupoInventario { Codigo = $"GI{Guid.NewGuid():N}"[..20].ToUpperInvariant(), Descripcion = "Grupo de prueba", CreatedBy = "test" };
        contexto.GruposInventario.Add(grupo);
        await contexto.SaveChangesAsync();
        return grupo.Id;
    }

    private async Task<Guid> SembrarSocioAsync(Guid? grupoNegocioId)
    {
        await using var contexto = _prueba.NuevoContexto();
        var socio = new SocioNegocio
        {
            Codigo = $"S{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            NombreComercial = "Socio de prueba del libro de valor",
            GrupoNegocioId = grupoNegocioId,
            CreatedBy = "test",
        };
        contexto.SociosNegocio.Add(socio);
        await contexto.SaveChangesAsync();
        return socio.Id;
    }
}
