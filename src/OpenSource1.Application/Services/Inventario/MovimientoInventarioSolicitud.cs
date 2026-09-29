using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Services.Inventario;

/// <summary>
/// Petición de registro de un movimiento en el libro de inventario (Fase 3). La consumen los posteos de
/// diarios (Fase 4), ventas (Fase 6) y la migración del stock legado (Task 3.6).
/// </summary>
/// <param name="Cantidad">SIEMPRE mayor que cero, en la unidad <paramref name="UnidadMedidaId"/>; el signo lo da <paramref name="EsEntrada"/>.</param>
/// <param name="CostoUnitario">
/// Obligatorio (&gt;= 0) en entradas que no son transferencia, expresado en la unidad BASE del producto. Ignorado
/// en salidas (el costo lo calcula el servicio con el promedio móvil).
/// </param>
public sealed record MovimientoInventarioSolicitud(
    Guid ProductoId,
    Guid AlmacenId,
    TipoMovimientoInventario TipoMovimiento,
    decimal Cantidad,
    bool EsEntrada,
    Guid UnidadMedidaId,
    decimal? CostoUnitario,
    DateOnly FechaRegistro,
    DateOnly FechaDocumento,
    TipoDocumentoInventario TipoDocumento,
    string? NumeroDocumento,
    int NumeroLineaDocumento,
    TipoOrigenMovimiento TipoOrigen,
    string ClaveOrigen,
    Guid? SocioNegocioId = null,
    decimal ImporteVenta = 0m);

/// <summary>Resultado de un registro: ids de las filas escritas, cantidad en unidad base e importe de costo (con signo).</summary>
public sealed record MovimientoRegistrado(long MovimientoProductoId, long MovimientoValorId, decimal CantidadBase, decimal ImporteCosto);
