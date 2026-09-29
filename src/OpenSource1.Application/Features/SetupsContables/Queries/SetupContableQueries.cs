using MediatR;
using OpenSource1.Application.Features.SetupsContables.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.SetupsContables.Queries;

public sealed record GetSetupGeneralByIdQuery(Guid Id) : IRequest<Result<SetupGeneralResponse>>;

public sealed record ListSetupsGeneralQuery(SetupContableSearchCriteria Search, PageRequest Paginacion)
    : IRequest<Result<PagedResult<SetupGeneralResponse>>>;

public sealed record GetSetupIvaByIdQuery(Guid Id) : IRequest<Result<SetupIvaResponse>>;

public sealed record ListSetupsIvaQuery(SetupContableSearchCriteria Search, PageRequest Paginacion)
    : IRequest<Result<PagedResult<SetupIvaResponse>>>;

public sealed record GetSetupInventarioByIdQuery(Guid Id) : IRequest<Result<SetupInventarioResponse>>;

public sealed record ListSetupsInventarioQuery(SetupContableSearchCriteria Search, PageRequest Paginacion)
    : IRequest<Result<PagedResult<SetupInventarioResponse>>>;
