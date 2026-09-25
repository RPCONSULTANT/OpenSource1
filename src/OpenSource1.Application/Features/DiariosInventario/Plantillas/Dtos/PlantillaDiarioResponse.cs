using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.DiariosInventario.Plantillas.Dtos;

/// <summary>DTO de lectura de una plantilla de diario. Sembradas y de solo lectura: no hay Create/Update/Delete.</summary>
public sealed class PlantillaDiarioResponse
{
    public Guid Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public TipoPlantillaDiario Tipo { get; init; }
    public Guid SerieId { get; init; }
}
