using MediatR;
using OpenSource1.Application.Features.Entradas.Dtos;
using OpenSource1.Application.Features.Entradas.Queries;

namespace OpenSource1.Application.Features.Entradas.Handlers;

public sealed class GetEntradaByIdQueryHandler(IEntradaReadRepository readRepository)
    : IRequestHandler<GetEntradaByIdQuery, EntradaResponse?>
{
    public Task<EntradaResponse?> Handle(GetEntradaByIdQuery request, CancellationToken cancellationToken) =>
        readRepository.GetByIdAsync(request.Id, cancellationToken);
}
