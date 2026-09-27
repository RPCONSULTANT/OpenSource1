using Dapper;
using OpenSource1.Application.Data;

namespace OpenSource1.Infrastructure.Services.Inventario;

/// <summary>
/// Proyección informativa <c>Producto.CostoUnitario</c> (Task 8.3): el costo promedio vigente a la ÚLTIMA fecha con
/// movimientos del producto, es decir <c>V / Q</c> con <c>V = Σ ImporteCosto</c> y <c>Q = Σ CantidadValorada</c> sobre
/// TODOS sus movimientos de valor (ajustes y redondeos incluidos, que llevan cantidad 0), redondeado a 4 decimales como
/// <see cref="CostoPromedioCalculadora.CostoPorUnidad"/>. Coincide con el costo medio de la vista de existencias
/// (valor / existencia) cuando toda la existencia está en un almacén. Si <c>Q &lt;= 0</c> conserva su valor (es el costo de
/// reserva del posteo; ver <see cref="CostoPromedioCalculadora.Calcular"/>), y también si el promedio cae fuera de
/// <c>[0, 1e14)</c> (un valor negativo con cantidad positiva o un desbordamiento de numeric(18,4) solo pueden venir de un
/// libro pendiente de ajuste; la siguiente pasada lo corregirá).
/// </summary>
/// <remarks>
/// La usan <see cref="RegistroMovimientosInventario"/> tras cada movimiento y <see cref="AjusteCostoInventario"/> al cerrar
/// cada producto (misma definición: ambos caminos dan el mismo valor). La migración <c>RecalcularCostoUnitario</c> aplica la
/// misma fórmula en SQL a los datos existentes. Debe llamarse dentro de la transacción del llamador y bajo el advisory lock
/// del producto (<see cref="BloqueoInventarioProducto"/>). UPDATE de UNA columna y solo si el valor cambia: aun así cambia el
/// <c>xmin</c> de la fila, así que una edición concurrente del maestro del producto que partió del <c>xmin</c> anterior recibe
/// 409 (conflicto de concurrencia), comportamiento ya aceptado en la Fase 3 para <c>CostoAjustado</c>.
/// </remarks>
internal static class ProyeccionCostoUnitario
{
    /// <summary>Máximo de numeric(18,4).</summary>
    private const decimal CostoMaximo = 99_999_999_999_999.9999m;

    /// <summary>Recalcula y guarda la proyección del producto si cambia. Devuelve el costo calculado o null si se conservó.</summary>
    public static async Task<decimal?> ActualizarAsync(IDbSession session, Guid productoId, CancellationToken ct)
    {
        var tx = session.CurrentTransaction;
        var (v, q) = await session.Connection.QuerySingleAsync<(decimal V, decimal Q)>(new CommandDefinition(
            """
            SELECT COALESCE(SUM("ImporteCosto"), 0) AS "V", COALESCE(SUM("CantidadValorada"), 0) AS "Q"
            FROM "MovimientosValor" WHERE "ProductoId" = @productoId
            """,
            new { productoId }, tx, cancellationToken: ct));

        if (CostoPromedioCalculadora.Calcular(v, q) is not { } promedio)
        {
            return null;
        }

        var costo = CostoPromedioCalculadora.CostoPorUnidad(promedio);
        if (costo < 0 || costo > CostoMaximo)
        {
            return null;
        }

        await session.Connection.ExecuteAsync(new CommandDefinition(
            """UPDATE "Productos" SET "CostoUnitario" = @costo WHERE "Id" = @productoId AND "CostoUnitario" <> @costo""",
            new { productoId, costo }, tx, cancellationToken: ct));
        return costo;
    }
}
