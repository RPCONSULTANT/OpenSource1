using OpenSource1.Core.Enums;

namespace OpenSource1.Core.Entities.Inventario;

/// <summary>
/// Línea de un <see cref="LoteDiario"/> (Fase 4): borrador de un movimiento de inventario, validado al
/// capturarse pero sin escribir nada en el libro todavía (eso lo hace el posteo de la Task 4.3 vía
/// <c>IRegistroMovimientosInventario</c>). Es un maestro con soft delete y concurrencia optimista.
/// </summary>
public sealed class LineaDiario : BaseEntity
{
    public Guid LoteDiarioId { get; set; }

    /// <summary>Asignado por el sistema: máximo vigente del lote + 10000 (10000 si no hay ninguna). Único junto con <see cref="LoteDiarioId"/>.</summary>
    public int NumeroLinea { get; set; }

    public DateOnly FechaRegistro { get; set; }
    public DateOnly FechaDocumento { get; set; }

    /// <summary>Opcional: si viene vacío, el movimiento posteado usa el número de registro (Task 4.3).</summary>
    public string? NumeroDocumento { get; set; }

    /// <summary>Permitido según el tipo de la plantilla: Articulo → AjustePositivo/AjusteNegativo; Reclasificacion → Transferencia.</summary>
    public TipoMovimientoInventario TipoMovimiento { get; set; }

    public Guid ProductoId { get; set; }
    public Guid AlmacenId { get; set; }

    /// <summary>Solo en Transferencia; debe ser distinto de <see cref="AlmacenId"/> y nulo en los demás tipos.</summary>
    public Guid? AlmacenDestinoId { get; set; }

    public Guid UnidadMedidaId { get; set; }

    /// <summary>Factor congelado al guardar la línea (vía <c>IConversionUnidadMedidaService.ObtenerConversionAsync</c>); el registro (Task 4.3) vuelve a obtenerlo y falla con <c>diario.factor_cambiado</c> si cambió.</summary>
    public decimal CantidadPorUnidadMedida { get; set; }

    /// <summary>Siempre positiva, en <see cref="UnidadMedidaId"/>; el signo lo da <see cref="TipoMovimiento"/>.</summary>
    public decimal Cantidad { get; set; }

    /// <summary>En la unidad BASE del producto (desviación acordada de la Fase 4). Obligatorio en AjustePositivo, nulo en AjusteNegativo/Transferencia.</summary>
    public decimal? CostoUnitario { get; set; }

    /// <summary>Calculado: <c>Round(Cantidad * factor * CostoUnitario, 4)</c>, o 0 si no aplica.</summary>
    public decimal ImporteCosto { get; set; }

    public string? Descripcion { get; set; }
}
