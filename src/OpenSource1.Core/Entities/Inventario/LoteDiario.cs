namespace OpenSource1.Core.Entities.Inventario;

/// <summary>
/// Lote de un diario de inventario (Fase 4): agrupa las <see cref="LineaDiario"/> que se registran juntas
/// (Task 4.3). Es un maestro con soft delete y concurrencia optimista (<c>xmin</c>), no un documento
/// posteado.
/// </summary>
public sealed class LoteDiario : BaseEntity
{
    public Guid PlantillaDiarioId { get; set; }

    /// <summary>Mayúsculas; único junto con <see cref="PlantillaDiarioId"/> (índice parcial, filas vivas).</summary>
    public required string Codigo { get; set; }

    public required string Nombre { get; set; }

    /// <summary>Serie a usar al registrar (Task 4.3); <see langword="null"/> = la de la plantilla.</summary>
    public Guid? SerieId { get; set; }

    /// <summary>Un lote bloqueado no admite altas/modificaciones/borrados de línea ni registro.</summary>
    public bool Bloqueado { get; set; }
}
