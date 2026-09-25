using OpenSource1.Application.Features.DiariosInventario.Plantillas.Dtos;

namespace OpenSource1.Application.Features.DiariosInventario.Plantillas;

public interface IPlantillaDiarioReadRepository
{
    /// <summary>Las dos plantillas sembradas, ordenadas por <c>Codigo</c>. Sin paginar: la lista nunca crece (no hay CRUD).</summary>
    Task<IReadOnlyList<PlantillaDiarioResponse>> ListAsync(CancellationToken cancellationToken = default);
}
