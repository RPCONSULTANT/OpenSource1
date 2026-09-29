using MediatR;
using OpenSource1.Application.Features.DiariosInventario.Registros.Dtos;
using OpenSource1.Application.Features.DiariosInventario.Registros.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.DiariosInventario.Registros.Handlers;

public sealed class ListRegistrosDiarioQueryHandler(IRegistroDiarioReadRepository readRepository)
    : IRequestHandler<ListRegistrosDiarioQuery, Result<PagedResult<RegistroDiarioResponse>>>
{
    public Task<Result<PagedResult<RegistroDiarioResponse>>> Handle(ListRegistrosDiarioQuery request, CancellationToken cancellationToken) =>
        readRepository.ListAsync(request.LoteDiarioId, request.Paginacion, cancellationToken);
}
