using OpenSource1.Application.Features.DiariosInventario.Lineas.Dtos;

namespace OpenSource1.Application.Features.DiariosInventario.Lineas;

public interface ILineaDiarioReadRepository
{
    Task<LineaDiarioResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Todas las líneas vivas del lote, ordenadas por <c>NumeroLinea</c>. Sin paginar: un lote es pequeño (tope 1000).</summary>
    Task<IReadOnlyList<LineaDiarioResponse>> ListByLoteAsync(Guid loteDiarioId, CancellationToken cancellationToken = default);
}
