using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Core.Enums;

namespace OpenSource1.Infrastructure.Services.Contabilidad;

/// <summary>
/// Guarda de uso de los grupos contables: un grupo está en uso si lo referencia un producto o un socio de negocio (Task 5.3) o una
/// fila de setup contable (Task 5.4, en cualquiera de sus dos ejes), todos NO borrados lógicamente, o si está congelado en un
/// movimiento de valor o contable (Task 5.5; los libros no tienen borrado lógico). Los índices de las FK sirven a
/// estas consultas. Consulta Dapper contra la sesión del scope (ve la transacción del llamador si la hay).
/// </summary>
public sealed class GrupoContableUsoService(IDbSession session) : IGrupoContableUsoService
{
    public Task<bool> EstaEnUsoAsync(TipoGrupoContable tipo, Guid grupoId, CancellationToken cancellationToken = default)
    {
        // Tablas y columnas salen de estos switch sobre el enum (literales de código), nunca de texto del usuario.
        (string Tabla, string Columna)[] maestros = tipo switch
        {
            // Task 6.2: los borradores de factura (cabecera y líneas vivas) congelan grupos que el posteo usará.
            TipoGrupoContable.Negocio =>
                [("SociosNegocio", "GrupoNegocioId"), ("SetupsContableGeneral", "GrupoNegocioId"), ("FacturasVentaBorrador", "GrupoNegocioId")],
            TipoGrupoContable.IvaNegocio =>
                [("SociosNegocio", "GrupoIvaNegocioId"), ("SetupsIva", "GrupoIvaNegocioId"), ("FacturasVentaBorrador", "GrupoIvaNegocioId")],
            TipoGrupoContable.Producto =>
                [("Productos", "GrupoProductoId"), ("SetupsContableGeneral", "GrupoProductoId"), ("LineasFacturaVentaBorrador", "GrupoProductoId")],
            TipoGrupoContable.IvaProducto =>
                [("Productos", "GrupoIvaProductoId"), ("SetupsIva", "GrupoIvaProductoId"), ("LineasFacturaVentaBorrador", "GrupoIvaProductoId")],
            TipoGrupoContable.Inventario =>
                [("Productos", "GrupoInventarioId"), ("SetupsInventario", "GrupoInventarioId"), ("LineasFacturaVentaBorrador", "GrupoInventarioId")],
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de grupo contable desconocido.")
        };

        // Referencias "de libro" (Task 5.5): grupos congelados en el libro de valor y en el libro contable. Sin filtro de
        // IsDeleted (los libros no lo tienen): un grupo ya usado en un movimiento no se puede borrar lógicamente, porque el
        // batch de costo (Task 5.6) y los informes derivan cuentas de él. Cada columna tiene su índice de FK.
        (string Tabla, string Columna)[] libros = tipo switch
        {
            TipoGrupoContable.Negocio => [("MovimientosValor", "GrupoNegocioId"), ("MovimientosContables", "GrupoNegocioId")],
            TipoGrupoContable.IvaNegocio => [("MovimientosContables", "GrupoIvaNegocioId")],
            TipoGrupoContable.Producto => [("MovimientosValor", "GrupoProductoId"), ("MovimientosContables", "GrupoProductoId")],
            TipoGrupoContable.IvaProducto => [("MovimientosContables", "GrupoIvaProductoId")],
            TipoGrupoContable.Inventario => [("MovimientosValor", "GrupoInventarioId")],
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de grupo contable desconocido.")
        };

        var sql = "SELECT " + string.Join(" OR ",
            maestros.Select(r => $"""EXISTS (SELECT 1 FROM "{r.Tabla}" WHERE "{r.Columna}" = @Id AND "IsDeleted" = false)""")
                .Concat(libros.Select(r => $"""EXISTS (SELECT 1 FROM "{r.Tabla}" WHERE "{r.Columna}" = @Id)""")));
        return ExisteAsync(sql, grupoId, cancellationToken);
    }

    public Task<bool> GrupoClienteContableEnUsoAsync(Guid grupoId, CancellationToken cancellationToken = default) =>
        ExisteAsync(
            """
            SELECT EXISTS (SELECT 1 FROM "SociosNegocio" WHERE "GrupoClienteContableId" = @Id AND "IsDeleted" = false)
                OR EXISTS (SELECT 1 FROM "FacturasVentaBorrador" WHERE "GrupoClienteContableId" = @Id AND "IsDeleted" = false)
            """,
            grupoId, cancellationToken);

    private async Task<bool> ExisteAsync(string sql, Guid id, CancellationToken cancellationToken)
    {
        await session.EnsureOpenAsync(cancellationToken);
        return await session.Connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { Id = id }, session.CurrentTransaction, cancellationToken: cancellationToken));
    }
}
