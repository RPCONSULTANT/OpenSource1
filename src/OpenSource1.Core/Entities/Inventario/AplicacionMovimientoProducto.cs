using OpenSource1.Core.Abstractions;

namespace OpenSource1.Core.Entities.Inventario;

/// <summary>
/// Aplicación FIFO de una salida contra una o más entradas (fracciones de <see cref="MovimientoProducto.Cantidad"/>
/// consumidas). Append-only, mismo trigger que el resto del libro. No hereda de <see cref="BaseEntity"/> por la
/// misma razón que <see cref="MovimientoProducto"/>.
/// </summary>
public sealed class AplicacionMovimientoProducto : IAggregateRoot
{
    public long Id { get; set; }
    public long MovimientoEntradaId { get; set; }
    public long MovimientoSalidaId { get; set; }

    /// <summary>numeric(18,6), siempre mayor que cero.</summary>
    public decimal Cantidad { get; set; }

    public DateOnly FechaRegistro { get; set; }
}
