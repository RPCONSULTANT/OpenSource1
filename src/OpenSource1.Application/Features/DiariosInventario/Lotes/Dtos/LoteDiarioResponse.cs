namespace OpenSource1.Application.Features.DiariosInventario.Lotes.Dtos;

/// <summary>
/// DTO de lectura de un lote de diario. <see cref="NumeroLineas"/> es un conteo derivado (para la UI, que
/// muestra cuántas líneas tiene el lote sin tener que pedirlas). <see cref="Xmin"/> es el token de
/// concurrencia optimista (Postgres <c>xmin</c>) que el cliente debe reenviar en el PUT.
/// </summary>
public sealed class LoteDiarioResponse
{
    public Guid Id { get; init; }
    public Guid PlantillaDiarioId { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public Guid? SerieId { get; init; }
    public bool Bloqueado { get; init; }
    public int NumeroLineas { get; init; }
    public long Xmin { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
}
