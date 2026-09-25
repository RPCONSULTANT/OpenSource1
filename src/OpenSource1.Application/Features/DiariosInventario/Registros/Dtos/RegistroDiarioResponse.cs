namespace OpenSource1.Application.Features.DiariosInventario.Registros.Dtos;

/// <summary>DTO de lectura de un registro de diario (append-only: sin <c>Xmin</c> ni campos de modificación).</summary>
public sealed class RegistroDiarioResponse
{
    public long Id { get; init; }
    public string NumeroRegistro { get; init; } = string.Empty;
    public Guid LoteDiarioId { get; init; }

    /// <summary>Código del lote (aunque esté borrado lógicamente: el registro sigue siendo historia).</summary>
    public string? LoteDiarioCodigo { get; init; }

    public long DesdeMovimientoProducto { get; init; }
    public long HastaMovimientoProducto { get; init; }
    public int Lineas { get; init; }
    public DateTime FechaCreacion { get; init; }
    public string CreadoPor { get; init; } = string.Empty;
}
