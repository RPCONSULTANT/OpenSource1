using MediatR;
using OpenSource1.Application.Features.GruposClienteContable.Dtos;
using OpenSource1.Application.Features.GruposClienteContable.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.GruposClienteContable.Handlers;

public sealed class GetGrupoClienteContableByIdQueryHandler(IGrupoClienteContableReadRepository readRepository)
    : IRequestHandler<GetGrupoClienteContableByIdQuery, Result<GrupoClienteContableResponse>>
{
    public async Task<Result<GrupoClienteContableResponse>> Handle(GetGrupoClienteContableByIdQuery request, CancellationToken cancellationToken)
    {
        var item = await readRepository.GetByIdAsync(request.Id, cancellationToken);
        return item is null
            ? Result<GrupoClienteContableResponse>.Fallo(GrupoClienteContableReglas.NoEncontrado())
            : Result<GrupoClienteContableResponse>.Exito(item);
    }
}

public sealed class ListGruposClienteContableQueryHandler(IGrupoClienteContableReadRepository readRepository)
    : IRequestHandler<ListGruposClienteContableQuery, Result<PagedResult<GrupoClienteContableResponse>>>
{
    public Task<Result<PagedResult<GrupoClienteContableResponse>>> Handle(ListGruposClienteContableQuery request, CancellationToken cancellationToken) =>
        readRepository.ListAsync(request.Search, request.Paginacion, cancellationToken);
}
