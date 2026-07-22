using OpenSource1.Application.Features.Clientes.Dtos;

namespace OpenSource1.Application.Features.Clientes;

public interface IClienteReadRepository
{
    Task<ClienteResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClienteResponse>> ListAsync(ClienteSearchCriteria search, CancellationToken cancellationToken = default);
}
