namespace OpenSource1.Infrastructure.Services.Inventario;

/// <summary>
/// Costo promedio móvil por día (desviación "Fórmula del promedio" de la Fase 3). Para una SALIDA con fecha <c>d</c>:
/// <c>Costo = V / Q</c>, con <c>V = SUM(ImporteCosto)</c> y <c>Q = SUM(CantidadValorada)</c> sobre todos los movimientos
/// de valor del producto con <c>FechaRegistro &lt; d</c> más los de entradas no transferencia con <c>FechaRegistro = d</c>
/// (las salidas del mismo día comparten el costo). Sin redondear: el redondeo se aplica al construir el importe.
/// </summary>
internal static class CostoPromedioCalculadora
{
    /// <summary>Parámetros <c>@productoId</c> y <c>@fecha</c>; columnas <c>"V"</c> y <c>"Q"</c>. 5 = <c>TipoMovimientoInventario.Transferencia</c>.</summary>
    public const string SumasSql = """
        SELECT COALESCE(SUM(v."ImporteCosto"), 0) AS "V", COALESCE(SUM(v."CantidadValorada"), 0) AS "Q"
        FROM "MovimientosValor" v
        WHERE v."ProductoId" = @productoId
          AND (v."FechaRegistro" < @fecha
               OR (v."FechaRegistro" = @fecha AND v."CantidadValorada" > 0 AND v."TipoMovimiento" <> 5));
        """;

    /// <summary><c>V / Q</c>, o null si <c>Q &lt;= 0</c> (entonces el llamador usa <c>Producto.CostoUnitario</c> y marca ajuste pendiente).</summary>
    public static decimal? Calcular(decimal valor, decimal cantidad) => cantidad > 0 ? valor / cantidad : null;

    /// <summary>Importe de costo (sin signo) de <paramref name="cantidadBase"/> unidades a <paramref name="costoUnitario"/>, a 4 decimales.</summary>
    public static decimal Importe(decimal cantidadBase, decimal costoUnitario) =>
        Math.Round(cantidadBase * costoUnitario, 4, MidpointRounding.AwayFromZero);

    /// <summary>Costo por unidad tal como se guarda en <c>MovimientosValor.CostoPorUnidad</c> (numeric(18,4)).</summary>
    public static decimal CostoPorUnidad(decimal costoUnitario) =>
        Math.Round(costoUnitario, 4, MidpointRounding.AwayFromZero);
}
