using Dapper;
using OpenSource1.Application.Data;

namespace OpenSource1.Infrastructure.Services.Inventario;

/// <summary>
/// Serialización por producto del libro de inventario (desviación de la Fase 3: advisory lock en lugar de
/// <c>SELECT ... FOR UPDATE</c> sobre <c>Productos</c>, para no bloquear la edición del maestro). Todo escritor del libro
/// de un producto (registro de movimientos, Task 3.4; ajuste de costo, Task 3.5) DEBE tomar exactamente esta clave,
/// o dejaría de excluirse mutuamente con los demás. También la toman (vía
/// <c>IRegistroMovimientosInventario.BloquearProductosAsync</c>) el borrado de un producto y el cambio de su unidad base,
/// antes de comprobar si tiene movimientos (Task 4.3). La clave del almacén, y el orden de adquisición producto -&gt;
/// almacén, están documentados en <see cref="BloqueoInventarioAlmacen"/>.
/// <para>
/// Orden GLOBAL de locks de todo escritor del libro de inventario (Fase 4, posteo de un diario): lote -&gt; líneas
/// (<c>FOR UPDATE</c> de <c>LotesDiario</c>/<c>LineasDiario</c>) -&gt; productos (esta clase, en orden de Guid) -&gt;
/// línea de serie (<c>FOR UPDATE</c> de <c>IGeneradorNumeroDocumento</c>) -&gt; almacenes (compartido,
/// <see cref="BloqueoInventarioAlmacen"/>). Respetar este orden en cualquier escritor nuevo es lo único que evita un
/// interbloqueo entre dos transacciones que tocan las mismas filas en órdenes distintos.
/// </para>
/// </summary>
internal static class BloqueoInventarioProducto
{
    /// <summary>Clave literal del bloqueo; se libera sola en el commit/rollback de la transacción.</summary>
    public const string Sql = "SELECT pg_advisory_xact_lock(hashtextextended(@productoId::text, 0))";

    /// <summary>Espera hasta obtener el bloqueo del producto. Requiere transacción activa (un xact lock sin ella se libera al acabar la sentencia).</summary>
    public static async Task AdquirirAsync(IDbSession session, Guid productoId, CancellationToken ct)
    {
        if (session.CurrentTransaction is null)
        {
            throw new InvalidOperationException("El bloqueo de inventario por producto requiere una transacción activa.");
        }

        await session.Connection.ExecuteAsync(
            new CommandDefinition(Sql, new { productoId }, session.CurrentTransaction, cancellationToken: ct));
    }
}
