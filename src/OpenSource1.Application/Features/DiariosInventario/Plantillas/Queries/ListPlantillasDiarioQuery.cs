using MediatR;
using OpenSource1.Application.Features.DiariosInventario.Plantillas.Dtos;

namespace OpenSource1.Application.Features.DiariosInventario.Plantillas.Queries;

public sealed record ListPlantillasDiarioQuery : IRequest<IReadOnlyList<PlantillaDiarioResponse>>;
