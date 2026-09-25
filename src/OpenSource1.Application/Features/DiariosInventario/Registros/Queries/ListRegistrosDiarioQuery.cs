using MediatR;
using OpenSource1.Application.Features.DiariosInventario.Registros.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.DiariosInventario.Registros.Queries;

/// <summary>Listado paginado de registros de diario, del más reciente al más antiguo; <paramref name="LoteDiarioId"/> opcional.</summary>
public sealed record ListRegistrosDiarioQuery(Guid? LoteDiarioId, PageRequest Paginacion)
    : IRequest<Result<PagedResult<RegistroDiarioResponse>>>;
