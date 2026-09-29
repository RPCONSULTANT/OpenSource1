using MediatR;
using OpenSource1.Application.Features.Contabilidad.Dtos;
using OpenSource1.Application.Features.Contabilidad.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Contabilidad.Handlers;

public sealed class ListRegistrosContablesQueryHandler(IContabilidadReadRepository readRepository)
    : IRequestHandler<ListRegistrosContablesQuery, Result<PagedResult<RegistroContableResponse>>>
{
    public Task<Result<PagedResult<RegistroContableResponse>>> Handle(
        ListRegistrosContablesQuery request, CancellationToken cancellationToken) =>
        readRepository.ListRegistrosAsync(request.Paginacion, cancellationToken);
}
