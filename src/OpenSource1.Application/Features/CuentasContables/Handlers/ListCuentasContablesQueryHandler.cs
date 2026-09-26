using MediatR;
using OpenSource1.Application.Features.CuentasContables.Dtos;
using OpenSource1.Application.Features.CuentasContables.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.CuentasContables.Handlers;

public sealed class ListCuentasContablesQueryHandler(ICuentaContableReadRepository readRepository)
    : IRequestHandler<ListCuentasContablesQuery, Result<PagedResult<CuentaContableResponse>>>
{
    public Task<Result<PagedResult<CuentaContableResponse>>> Handle(ListCuentasContablesQuery request, CancellationToken cancellationToken) =>
        readRepository.ListAsync(request.Search, request.Paginacion, cancellationToken);
}
