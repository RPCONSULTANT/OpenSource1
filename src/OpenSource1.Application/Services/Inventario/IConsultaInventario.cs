namespace OpenSource1.Application.Services.Inventario;

/// <summary>Existencia de un producto en un almacén (suma de <c>MovimientosProducto.Cantidad</c>, en unidad base).</summary>
public sealed record ExistenciaAlmacen(Guid AlmacenId, string AlmacenCodigo, string AlmacenNombre, decimal Existencia);

/// <summary>
/// Consultas derivadas del libro de inventario. La existencia NO se guarda en el maestro: se calcula
/// sumando movimientos. Usa la conexión (y la transacción, si la hay) de la sesión del scope.
/// </summary>
public interface IConsultaInventario
{
    /// <summary>Existencia en unidad base; <paramref name="almacenId"/> null = todos los almacenes; <paramref name="fecha"/> null = sin corte.</summary>
    Task<decimal> ExistenciaAsync(Guid productoId, Guid? almacenId, DateOnly? fecha, CancellationToken ct = default);

    /// <summary>Existencia por cada almacén que tiene movimientos del producto, ordenada por código de almacén.</summary>
    Task<IReadOnlyList<ExistenciaAlmacen>> ExistenciasPorAlmacenAsync(Guid productoId, CancellationToken ct = default);

    /// <summary>
    /// Costo promedio vigente para una SALIDA con fecha <paramref name="fecha"/> (fórmula de las desviaciones de la
    /// Fase 3: todos los movimientos de valor anteriores a la fecha más las entradas no transferencia del mismo día);
    /// null si la cantidad valorada Q es &lt;= 0. Sin redondear.
    /// </summary>
    Task<decimal?> CostoPromedioAsync(Guid productoId, DateOnly fecha, CancellationToken ct = default);
}
