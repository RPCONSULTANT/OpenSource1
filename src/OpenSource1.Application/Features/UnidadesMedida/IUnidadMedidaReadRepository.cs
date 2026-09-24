using OpenSource1.Application.Features.UnidadesMedida.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.UnidadesMedida;

public interface IUnidadMedidaReadRepository
{
    Task<UnidadMedidaResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<UnidadMedidaResponse>>> ListAsync(
        UnidadMedidaSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default);
}
