using MediatR;
using OpenSource1.Application.Features.Clientes.Queries;
using OpenSource1.Application.Features.Clientes.Dtos;

namespace OpenSource1.Application.Features.Clientes.Handlers;

public sealed class ListClientesQueryHandler(IClienteReadRepository readRepository) : IRequestHandler<ListClientesQuery, IReadOnlyList<ClienteResponse>>
{
    public Task<IReadOnlyList<ClienteResponse>> Handle(ListClientesQuery request, CancellationToken cancellationToken) => readRepository.ListAsync(request.Search, cancellationToken);
}
