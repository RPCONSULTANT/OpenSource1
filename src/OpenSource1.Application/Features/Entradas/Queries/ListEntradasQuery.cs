using MediatR;
using OpenSource1.Application.Features.Entradas.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Entradas.Queries;

public sealed record ListEntradasQuery(PageRequest Paginacion) : IRequest<Result<PagedResult<EntradaResponse>>>;
