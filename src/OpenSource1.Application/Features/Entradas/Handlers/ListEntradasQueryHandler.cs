using MediatR;
using OpenSource1.Application.Features.Entradas.Dtos;
using OpenSource1.Application.Features.Entradas.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Entradas.Handlers;

public sealed class ListEntradasQueryHandler(IEntradaReadRepository readRepository)
    : IRequestHandler<ListEntradasQuery, Result<PagedResult<EntradaResponse>>>
{
    public Task<Result<PagedResult<EntradaResponse>>> Handle(ListEntradasQuery request, CancellationToken cancellationToken) =>
        readRepository.ListAsync(request.Paginacion, cancellationToken);
}
