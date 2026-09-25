using MediatR;
using OpenSource1.Application.Features.Clientes.Queries;
using OpenSource1.Application.Features.Clientes.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Clientes.Handlers;

public sealed class ListClientesQueryHandler(IClienteReadRepository readRepository)
    : IRequestHandler<ListClientesQuery, Result<PagedResult<ClienteResponse>>>
{
    public Task<Result<PagedResult<ClienteResponse>>> Handle(ListClientesQuery request, CancellationToken cancellationToken) =>
        readRepository.ListAsync(request.Search, request.Paginacion, cancellationToken);
}
