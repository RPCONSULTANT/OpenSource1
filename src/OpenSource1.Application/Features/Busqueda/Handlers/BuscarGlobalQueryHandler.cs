using MediatR;
using OpenSource1.Application.Features.Busqueda.Dtos;
using OpenSource1.Application.Features.Busqueda.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Busqueda.Handlers;

public sealed class BuscarGlobalQueryHandler(IBusquedaGlobalRepository repositorio)
    : IRequestHandler<BuscarGlobalQuery, Result<BusquedaGlobalResponse>>
{
    public async Task<Result<BusquedaGlobalResponse>> Handle(BuscarGlobalQuery request, CancellationToken cancellationToken)
    {
        var termino = request.Q?.Trim() ?? string.Empty;
        if (termino.Length is < BuscarGlobalQuery.LongitudMinima or > BuscarGlobalQuery.LongitudMaxima)
        {
            return Result<BusquedaGlobalResponse>.Fallo(new Error(
                "busqueda.q_invalida",
                $"El texto a buscar debe tener entre {BuscarGlobalQuery.LongitudMinima} y {BuscarGlobalQuery.LongitudMaxima} caracteres.",
                "q"));
        }

        if (request.Limite is < 1 or > BuscarGlobalQuery.LimiteMaximo)
        {
            return Result<BusquedaGlobalResponse>.Fallo(new Error(
                "busqueda.limite_invalido", $"El límite por tipo debe estar entre 1 y {BuscarGlobalQuery.LimiteMaximo}.", "limite"));
        }

        return Result<BusquedaGlobalResponse>.Exito(await repositorio.BuscarAsync(termino, request.Limite, cancellationToken));
    }
}
