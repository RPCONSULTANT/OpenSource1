using MediatR;
using OpenSource1.Application.Features.Contabilidad.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Contabilidad.Queries;

public sealed record ListRegistrosContablesQuery(PageRequest Paginacion) : IRequest<Result<PagedResult<RegistroContableResponse>>>;
