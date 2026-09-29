namespace OpenSource1.Core.Entities.Inventario;

/// <summary>
/// Aplicación FIFO de una salida contra una o más entradas (fracciones de <see cref="MovimientoProducto.Cantidad"/>
/// consumidas). Append-only, mismo trigger que el resto del libro. No hereda de <see cref="BaseEntity"/> por la
/// misma razón que <see cref="MovimientoProducto"/>.
/// </summary>
/// <remarks>
/// Deliberadamente NO implementa <c>IAggregateRoot</c> (revisión final de la Fase 3): mismo motivo que
/// <see cref="MovimientoValor"/>: sin ella, <c>IUnitOfWork.Repository&lt;T&gt;()</c>/<c>GenericRepository</c> no
/// compilan con esta entidad. Nada en el código la usa así; EF la sigue mapeando igual.
/// </remarks>
public sealed class AplicacionMovimientoProducto
{
    public long Id { get; set; }
    public long MovimientoEntradaId { get; set; }
    public long MovimientoSalidaId { get; set; }

    /// <summary>numeric(18,6), siempre mayor que cero.</summary>
    public decimal Cantidad { get; set; }

    public DateOnly FechaRegistro { get; set; }
}
