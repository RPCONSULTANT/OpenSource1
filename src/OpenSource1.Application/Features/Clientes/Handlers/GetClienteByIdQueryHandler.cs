using MediatR;
using OpenSource1.Application.Features.Clientes.Queries;
using OpenSource1.Application.Features.Clientes.Dtos;

namespace OpenSource1.Application.Features.Clientes.Handlers;

public sealed class GetClienteByIdQueryHandler(IClienteReadRepository readRepository) : IRequestHandler<GetClienteByIdQuery, ClienteResponse?>
{
    public Task<ClienteResponse?> Handle(GetClienteByIdQuery request, CancellationToken cancellationToken) => readRepository.GetByIdAsync(request.Id, cancellationToken);
}
