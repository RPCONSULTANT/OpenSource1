namespace OpenSource1.Application.Features.Productos;

/// <summary>
/// <c>Existencia</c> reemplaza al antiguo <c>Stock</c> (Task 3.6), pero NO con la misma semántica de filtro exacto ("=")
/// que el resto de los campos numéricos: filtra "mayor o igual que" (<c>&gt;=</c>), porque sobre una cantidad derivada y
/// con hasta 6 decimales una igualdad exacta es poco útil. <c>StockState</c> es independiente y vale <c>"all"</c>
/// (o vacío), <c>"with"</c> (existencia &gt; 0) o <c>"without"</c> (existencia &lt;= 0); cualquier otro valor se trata
/// como <c>"all"</c>.
/// </summary>
public sealed record ProductoSearchCriteria(
    string? Codigo,
    string? Nombre,
    string? CategoriaCodigo,
    string? CategoriaNombre,
    string? UnidadMedidaCodigo,
    string? UnidadMedidaNombre,
    string? PrecioVenta,
    string? Existencia,
    string? StockState = null);
