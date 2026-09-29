using MediatR;
using OpenSource1.Application.Features.Busqueda.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Busqueda.Queries;

public sealed record BuscarGlobalQuery(string? Q, int Limite = BuscarGlobalQuery.LimitePorDefecto)
    : IRequest<Result<BusquedaGlobalResponse>>
{
    public const int LimitePorDefecto = 5;
    public const int LimiteMaximo = 20;
    public const int LongitudMinima = 2;
    public const int LongitudMaxima = 100;
}
