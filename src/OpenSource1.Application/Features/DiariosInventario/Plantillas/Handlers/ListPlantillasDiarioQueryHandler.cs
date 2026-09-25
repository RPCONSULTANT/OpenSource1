using MediatR;
using OpenSource1.Application.Features.DiariosInventario.Plantillas.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Plantillas.Queries;

namespace OpenSource1.Application.Features.DiariosInventario.Plantillas.Handlers;

public sealed class ListPlantillasDiarioQueryHandler(IPlantillaDiarioReadRepository readRepository)
    : IRequestHandler<ListPlantillasDiarioQuery, IReadOnlyList<PlantillaDiarioResponse>>
{
    public Task<IReadOnlyList<PlantillaDiarioResponse>> Handle(ListPlantillasDiarioQuery request, CancellationToken cancellationToken) =>
        readRepository.ListAsync(cancellationToken);
}
