using MediatR;
using OpenSource1.Application.Features.GruposClienteContable.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.GruposClienteContable.Queries;

public sealed record GetGrupoClienteContableByIdQuery(Guid Id) : IRequest<Result<GrupoClienteContableResponse>>;

public sealed record ListGruposClienteContableQuery(GrupoClienteContableSearchCriteria Search, PageRequest Paginacion)
    : IRequest<Result<PagedResult<GrupoClienteContableResponse>>>;
