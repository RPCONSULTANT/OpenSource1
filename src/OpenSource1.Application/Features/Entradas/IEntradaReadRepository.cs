using OpenSource1.Application.Features.Entradas.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Entradas;

public interface IEntradaReadRepository
{
    Task<EntradaResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<EntradaResponse>>> ListAsync(
        PageRequest paginacion, CancellationToken cancellationToken = default);
}
