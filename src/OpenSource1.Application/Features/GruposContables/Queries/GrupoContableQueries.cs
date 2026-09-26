using MediatR;
using OpenSource1.Application.Features.GruposContables.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.GruposContables.Queries;

public sealed record GetGrupoContableByIdQuery(TipoGrupoContable Tipo, Guid Id) : IRequest<Result<GrupoContableResponse>>;

public sealed record ListGruposContablesQuery(TipoGrupoContable Tipo, GrupoContableSearchCriteria Search, PageRequest Paginacion)
    : IRequest<Result<PagedResult<GrupoContableResponse>>>;
