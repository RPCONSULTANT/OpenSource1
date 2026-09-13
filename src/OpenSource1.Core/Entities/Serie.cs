namespace OpenSource1.Core.Entities;

/// <summary>
/// Serie de numeración de documentos (p. ej. facturas, diarios de inventario). Agrupa una o
/// más <see cref="LineaSerie"/> que definen los rangos numéricos vigentes por fecha.
/// </summary>
public sealed class Serie : BaseEntity
{
    public required string Codigo { get; set; }
    public required string Descripcion { get; set; }

    /// <summary>
    /// Si es <c>true</c>, la serie tolera huecos en la numeración (p. ej. permite emitir sin
    /// bloqueo de fila). Esta tarea no implementa todavía ese camino sin bloqueo — ver el XML
    /// doc de <see cref="Data.IGeneradorNumeroDocumento"/> en OpenSource1.Application — pero el
    /// campo se modela desde ahora para no requerir una migración adicional cuando se use.
    /// </summary>
    public bool PermiteHuecos { get; set; }

    public bool PorDefecto { get; set; }
}
