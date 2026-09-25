namespace OpenSource1.Application.Features.Productos;

/// <summary>
/// <c>Existencia</c> reemplaza al antiguo <c>Stock</c> (Task 3.6) con la misma semántica de filtro exacto que tenían el resto
/// de los campos numéricos, pero comparando "mayor o igual que" (no "="): sobre una cantidad derivada y con decimales,
/// filtrar por igualdad exacta es poco útil. <c>StockState</c> es independiente y vale <c>"all"</c> (o vacío), <c>"with"</c>
/// (existencia &gt; 0) o <c>"without"</c> (existencia &lt;= 0); cualquier otro valor se trata como <c>"all"</c>.
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
