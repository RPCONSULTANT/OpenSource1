using MediatR;
using OpenSource1.Application.Features.MovimientosCliente.Dtos;
using OpenSource1.Application.Features.MovimientosCliente.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.MovimientosCliente.Handlers;

internal static class MovimientoClienteErrores
{
    /// <summary>El socio es el Id de la RUTA (<c>api/clientes/{id}</c>), no una referencia del cuerpo: 404.</summary>
    public static Error SocioNoEncontrado() =>
        new("socio_negocio.no_encontrado", "No se encontró el socio de negocio solicitado.", "Id");
}

public sealed class ListMovimientosClienteQueryHandler(IMovimientoClienteReadRepository readRepository)
    : IRequestHandler<ListMovimientosClienteQuery, Result<PagedResult<MovimientoClienteResponse>>>
{
    public async Task<Result<PagedResult<MovimientoClienteResponse>>> Handle(
        ListMovimientosClienteQuery request, CancellationToken cancellationToken)
    {
        if (!await readRepository.ExisteSocioAsync(request.Search.SocioNegocioId, cancellationToken))
        {
            return Result<PagedResult<MovimientoClienteResponse>>.Fallo(MovimientoClienteErrores.SocioNoEncontrado());
        }

        if (request.Search is { Desde: { } desde, Hasta: { } hasta } && desde > hasta)
        {
            return Result<PagedResult<MovimientoClienteResponse>>.Fallo(new Error(
                "movimiento_cliente.rango_fechas_invalido", "La fecha 'desde' no puede ser posterior a 'hasta'.", "Desde"));
        }

        return Result<PagedResult<MovimientoClienteResponse>>.Exito(
            await readRepository.ListAsync(request.Search, request.Paginacion, cancellationToken));
    }
}

public sealed class GetSaldoClienteQueryHandler(IMovimientoClienteReadRepository readRepository)
    : IRequestHandler<GetSaldoClienteQuery, Result<SaldoClienteResponse>>
{
    public async Task<Result<SaldoClienteResponse>> Handle(GetSaldoClienteQuery request, CancellationToken cancellationToken)
    {
        if (!await readRepository.ExisteSocioAsync(request.SocioNegocioId, cancellationToken))
        {
            return Result<SaldoClienteResponse>.Fallo(MovimientoClienteErrores.SocioNoEncontrado());
        }

        return Result<SaldoClienteResponse>.Exito(await readRepository.GetSaldoAsync(request.SocioNegocioId, cancellationToken));
    }
}

public sealed class ListMovimientosAbiertosClienteQueryHandler(IMovimientoClienteReadRepository readRepository)
    : IRequestHandler<ListMovimientosAbiertosClienteQuery, Result<IReadOnlyList<MovimientoClienteResponse>>>
{
    public async Task<Result<IReadOnlyList<MovimientoClienteResponse>>> Handle(
        ListMovimientosAbiertosClienteQuery request, CancellationToken cancellationToken)
    {
        if (!await readRepository.ExisteSocioAsync(request.SocioNegocioId, cancellationToken))
        {
            return Result<IReadOnlyList<MovimientoClienteResponse>>.Fallo(MovimientoClienteErrores.SocioNoEncontrado());
        }

        return Result<IReadOnlyList<MovimientoClienteResponse>>.Exito(
            await readRepository.ListAbiertosAsync(request.SocioNegocioId, cancellationToken));
    }
}
