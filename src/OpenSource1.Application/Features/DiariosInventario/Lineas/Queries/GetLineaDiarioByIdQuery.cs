using MediatR;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.DiariosInventario.Lineas.Queries;

public sealed record GetLineaDiarioByIdQuery(Guid Id) : IRequest<Result<LineaDiarioResponse>>;
