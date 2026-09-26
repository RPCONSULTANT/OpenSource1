using MediatR;
using OpenSource1.Application.Features.GruposContables.Dtos;
using OpenSource1.Application.Features.GruposContables.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.GruposContables.Handlers;

public sealed class GetGrupoContableByIdQueryHandler(IGrupoContableReadRepository readRepository)
    : IRequestHandler<GetGrupoContableByIdQuery, Result<GrupoContableResponse>>
{
    public async Task<Result<GrupoContableResponse>> Handle(GetGrupoContableByIdQuery request, CancellationToken cancellationToken)
    {
        if (!TiposGrupoContable.EsValido(request.Tipo))
        {
            return Result<GrupoContableResponse>.Fallo(GrupoContableDespacho.ErrorTipoInvalido());
        }

        var item = await readRepository.GetByIdAsync(request.Tipo, request.Id, cancellationToken);
        return item is null
            ? Result<GrupoContableResponse>.Fallo(GrupoContableDespacho.ErrorNoEncontrado(request.Tipo))
            : Result<GrupoContableResponse>.Exito(item);
    }
}

public sealed class ListGruposContablesQueryHandler(IGrupoContableReadRepository readRepository)
    : IRequestHandler<ListGruposContablesQuery, Result<PagedResult<GrupoContableResponse>>>
{
    public Task<Result<PagedResult<GrupoContableResponse>>> Handle(ListGruposContablesQuery request, CancellationToken cancellationToken) =>
        TiposGrupoContable.EsValido(request.Tipo)
            ? readRepository.ListAsync(request.Tipo, request.Search, request.Paginacion, cancellationToken)
            : Task.FromResult(Result<PagedResult<GrupoContableResponse>>.Fallo(GrupoContableDespacho.ErrorTipoInvalido()));
}
