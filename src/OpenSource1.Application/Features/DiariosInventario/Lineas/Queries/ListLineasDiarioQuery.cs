using MediatR;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Dtos;

namespace OpenSource1.Application.Features.DiariosInventario.Lineas.Queries;

/// <summary>Todas las líneas vivas de un lote, ordenadas por <c>NumeroLinea</c>. Sin paginar (tope 1000 por lote).</summary>
public sealed record ListLineasDiarioQuery(Guid LoteDiarioId) : IRequest<IReadOnlyList<LineaDiarioResponse>>;
