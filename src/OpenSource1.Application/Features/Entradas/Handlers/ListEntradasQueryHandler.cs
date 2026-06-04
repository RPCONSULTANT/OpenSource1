using MediatR;
using OpenSource1.Application.Features.Entradas.Dtos;
using OpenSource1.Application.Features.Entradas.Queries;

namespace OpenSource1.Application.Features.Entradas.Handlers;

public sealed class ListEntradasQueryHandler(IEntradaReadRepository readRepository)
    : IRequestHandler<ListEntradasQuery, IReadOnlyList<EntradaResponse>>
{
    public Task<IReadOnlyList<EntradaResponse>> Handle(ListEntradasQuery request, CancellationToken cancellationToken) =>
        readRepository.ListAsync(cancellationToken);
}
