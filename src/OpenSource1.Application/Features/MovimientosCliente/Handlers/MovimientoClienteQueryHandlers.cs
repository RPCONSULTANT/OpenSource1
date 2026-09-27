using MediatR;
using OpenSource1.Application.Common;
using OpenSource1.Application.Features.MovimientosCliente.Dtos;
using OpenSource1.Application.Features.MovimientosCliente.Queries;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

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

        // Review Focus 5: filtros inválidos → 400 con el campo (todos a la vez), nunca 500.
        var errores = new List<Error>();
        if (request.Search is { Desde: { } desde, Hasta: { } hasta } && desde > hasta)
        {
            errores.Add(new Error(
                "movimiento_cliente.rango_fechas_invalido", "La fecha 'desde' no puede ser posterior a 'hasta'.", "Desde"));
        }

        if (request.Search.TipoDocumento is { } tipo
            && !(tipo is >= short.MinValue and <= short.MaxValue && Enum.IsDefined(typeof(TipoDocumentoCliente), (short)tipo)))
        {
            errores.Add(new Error(
                "movimiento_cliente.tipo_documento_invalido",
                $"El tipo de documento {tipo} no es válido (1 Factura, 2 Nota de crédito, 3 Pago, 4 Ajuste).",
                "TipoDocumento"));
        }

        if (errores.Count > 0)
        {
            return Result<PagedResult<MovimientoClienteResponse>>.Fallo([.. errores]);
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

/// <summary>
/// Estado de cuenta (Task 7.3). Fecha de corte por defecto: hoy UTC (mismo criterio de "hoy" que el resto de handlers). Sin
/// validaciones: la página se normaliza y una página enorme da 200 con página vacía (<see cref="PageRequest.Offset"/>); el
/// socio del filtro no es entidad de ruta (inexistente = página vacía).
/// </summary>
public sealed class GetEstadoCuentaQueryHandler(IMovimientoClienteReadRepository readRepository)
    : IRequestHandler<GetEstadoCuentaQuery, Result<EstadoCuentaResponse>>
{
    public async Task<Result<EstadoCuentaResponse>> Handle(GetEstadoCuentaQuery request, CancellationToken cancellationToken)
    {
        var criterios = request.Criterios with
        {
            FechaCorte = request.Criterios.FechaCorte ?? DateOnly.FromDateTime(DateTime.UtcNow)
        };
        return Result<EstadoCuentaResponse>.Exito(
            await readRepository.GetEstadoCuentaAsync(criterios, request.Paginacion, cancellationToken));
    }
}
