using MediatR;
using OpenSource1.Application.Features.Clientes.Dtos;

namespace OpenSource1.Application.Features.Clientes.Queries;

public sealed record ListClientesQuery(ClienteSearchCriteria Search) : IRequest<IReadOnlyList<ClienteResponse>>;
