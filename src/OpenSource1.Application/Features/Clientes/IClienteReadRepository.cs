using OpenSource1.Application.Features.Clientes.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Clientes;

public interface IClienteReadRepository
{
    Task<ClienteResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<ClienteResponse>>> ListAsync(
        ClienteSearchCriteria search, PageRequest paginacion, CancellationToken cancellationToken = default);
}
