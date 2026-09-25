namespace OpenSource1.Application.Features.DiariosInventario.Lotes;

public sealed record LoteDiarioSearchCriteria(Guid? PlantillaDiarioId, string? Codigo, string? Nombre);
