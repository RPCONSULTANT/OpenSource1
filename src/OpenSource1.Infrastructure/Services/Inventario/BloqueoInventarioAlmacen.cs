using Dapper;
using OpenSource1.Application.Data;

namespace OpenSource1.Infrastructure.Services.Inventario;

/// <summary>
/// Bloqueo transaccional por almacén del libro de inventario (desviación de la Fase 4, pendiente de la Fase 3).
/// <see cref="RegistroMovimientosInventario.RegistrarAsync"/> lo toma en modo COMPARTIDO, después del de producto, para el
/// almacén de cada movimiento (dos registros no se esperan entre sí); el borrado de un almacén lo toma en modo EXCLUSIVO
/// antes de comprobar si tiene movimientos. Así nunca se escribe un movimiento en un almacén borrado.
/// <para>
/// Claves de advisory lock del inventario (ambas con <c>hashtextextended(..., 0)</c>, espacio de claves bigint único):
/// <list type="bullet">
/// <item>Producto (<see cref="BloqueoInventarioProducto"/>): <c>hashtextextended(productoId::text, 0)</c>.</item>
/// <item>Almacén (esta clase): <c>hashtextextended('almacen:' || almacenId::text, 0)</c>. El prefijo evita que la clave
/// de un almacén coincida con la de un producto aunque ambos Guid fueran iguales.</item>
/// </list>
/// Orden de adquisición para todo escritor: productos (en orden de Guid) y después almacenes; el borrado de almacén solo
/// toma su almacén, así que no hay ciclo posible.
/// </para>
/// </summary>
internal static class BloqueoInventarioAlmacen
{
    public const string SqlCompartido =
        "SELECT pg_advisory_xact_lock_shared(hashtextextended('almacen:' || @almacenId::text, 0))";

    public const string SqlExclusivo =
        "SELECT pg_advisory_xact_lock(hashtextextended('almacen:' || @almacenId::text, 0))";

    /// <summary>Modo compartido (registro de movimientos). Requiere transacción activa.</summary>
    public static Task AdquirirCompartidoAsync(IDbSession session, Guid almacenId, CancellationToken ct) =>
        AdquirirAsync(session, SqlCompartido, almacenId, ct);

    /// <summary>Modo exclusivo (borrado del almacén). Requiere transacción activa.</summary>
    public static Task AdquirirExclusivoAsync(IDbSession session, Guid almacenId, CancellationToken ct) =>
        AdquirirAsync(session, SqlExclusivo, almacenId, ct);

    private static async Task AdquirirAsync(IDbSession session, string sql, Guid almacenId, CancellationToken ct)
    {
        if (session.CurrentTransaction is null)
        {
            throw new InvalidOperationException("El bloqueo de inventario por almacén requiere una transacción activa.");
        }

        await session.Connection.ExecuteAsync(
            new CommandDefinition(sql, new { almacenId }, session.CurrentTransaction, cancellationToken: ct));
    }
}
