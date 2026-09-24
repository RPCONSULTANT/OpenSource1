using MediatR;
using OpenSource1.Application.Features.UnidadesMedida.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.UnidadesMedida.Queries;

public sealed record ListUnidadesMedidaQuery(UnidadMedidaSearchCriteria Search, PageRequest Paginacion)
    : IRequest<Result<PagedResult<UnidadMedidaResponse>>>;
