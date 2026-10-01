using MediatR;
using OpenSource1.Application.Data;
using OpenSource1.Application.Features.Series.Dtos;
using OpenSource1.Application.Features.Series.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Series.Handlers;

public sealed class ListSeriesQueryHandler(ISerieReadRepository lectura) : IRequestHandler<ListSeriesQuery, Result<PagedResult<SerieResponse>>>
{
    public Task<Result<PagedResult<SerieResponse>>> Handle(ListSeriesQuery request, CancellationToken cancellationToken) =>
        lectura.ListAsync(request.Search, request.Paginacion, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
}

public sealed class GetSerieByIdQueryHandler(ISerieReadRepository lectura) : IRequestHandler<GetSerieByIdQuery, Result<SerieDetalleResponse>>
{
    public async Task<Result<SerieDetalleResponse>> Handle(GetSerieByIdQuery request, CancellationToken cancellationToken) =>
        await lectura.GetByIdAsync(request.Id, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken) is { } detalle
            ? Result<SerieDetalleResponse>.Exito(detalle)
            : Result<SerieDetalleResponse>.Fallo(SerieReglas.NoEncontrada());
}

/// <summary>Vista previa del próximo número (sin bloqueo ni reserva). Una serie inexistente de la RUTA es 404.</summary>
public sealed class GetProximoNumeroQueryHandler(IGeneradorNumeroDocumento generador)
    : IRequestHandler<GetProximoNumeroQuery, Result<ProximoNumeroResponse>>
{
    public async Task<Result<ProximoNumeroResponse>> Handle(GetProximoNumeroQuery request, CancellationToken cancellationToken)
    {
        var proximo = await generador.ProximoNumeroAsync(
            request.SerieId, request.Fecha ?? DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken: cancellationToken);
        if (proximo.TryObtenerValor(out var numero))
        {
            return Result<ProximoNumeroResponse>.Exito(new ProximoNumeroResponse(numero.Numero, numero.Aviso));
        }

        return proximo.Errores[0].Codigo == "numeracion.serie_inexistente"
            ? Result<ProximoNumeroResponse>.Fallo(SerieReglas.NoEncontrada())
            : Result<ProximoNumeroResponse>.Fallo(proximo);
    }
}
