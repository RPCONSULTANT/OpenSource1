using MediatR;
using OpenSource1.Application.Features.Entradas.Dtos;

namespace OpenSource1.Application.Features.Entradas.Queries;

public sealed record ListEntradasQuery : IRequest<IReadOnlyList<EntradaResponse>>;
