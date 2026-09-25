using MediatR;
using OpenSource1.Application.Features.Clientes.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Clientes.Queries;

public sealed record ListClientesQuery(ClienteSearchCriteria Search, PageRequest Paginacion)
    : IRequest<Result<PagedResult<ClienteResponse>>>;
