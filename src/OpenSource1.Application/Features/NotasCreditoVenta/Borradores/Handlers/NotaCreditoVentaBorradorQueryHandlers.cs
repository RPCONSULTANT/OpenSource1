using MediatR;
using OpenSource1.Application.Features.FacturasVenta.Calculo;
using OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Dtos;
using OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Queries;
using OpenSource1.Application.Features.NotasCreditoVenta.Posteo;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Handlers;

public sealed class GetNotaCreditoVentaBorradorByIdQueryHandler(INotaCreditoVentaBorradorReadRepository readRepository)
    : IRequestHandler<GetNotaCreditoVentaBorradorByIdQuery, Result<NotaCreditoVentaBorradorResponse>>
{
    public async Task<Result<NotaCreditoVentaBorradorResponse>> Handle(
        GetNotaCreditoVentaBorradorByIdQuery request, CancellationToken cancellationToken)
    {
        var item = await readRepository.GetByIdAsync(request.Id, cancellationToken);
        return item is null
            ? Result<NotaCreditoVentaBorradorResponse>.Fallo(NotaCreditoVentaErrores.BorradorNoEncontrado())
            : Result<NotaCreditoVentaBorradorResponse>.Exito(item);
    }
}

public sealed class ListNotasCreditoVentaBorradorQueryHandler(INotaCreditoVentaBorradorReadRepository readRepository)
    : IRequestHandler<ListNotasCreditoVentaBorradorQuery, Result<PagedResult<NotaCreditoVentaBorradorResponse>>>
{
    public async Task<Result<PagedResult<NotaCreditoVentaBorradorResponse>>> Handle(
        ListNotasCreditoVentaBorradorQuery request, CancellationToken cancellationToken) =>
        Result<PagedResult<NotaCreditoVentaBorradorResponse>>.Exito(
            await readRepository.ListAsync(request.Search, request.Paginacion, cancellationToken));
}

public sealed class ListLineasNotaCreditoVentaBorradorQueryHandler(INotaCreditoVentaBorradorReadRepository readRepository)
    : IRequestHandler<ListLineasNotaCreditoVentaBorradorQuery, Result<IReadOnlyList<LineaNotaCreditoVentaBorradorResponse>>>
{
    public async Task<Result<IReadOnlyList<LineaNotaCreditoVentaBorradorResponse>>> Handle(
        ListLineasNotaCreditoVentaBorradorQuery request, CancellationToken cancellationToken)
    {
        if (await readRepository.GetByIdAsync(request.NotaCreditoVentaBorradorId, cancellationToken) is null)
        {
            return Result<IReadOnlyList<LineaNotaCreditoVentaBorradorResponse>>.Fallo(
                NotaCreditoVentaErrores.BorradorNoEncontrado("NotaCreditoVentaBorradorId"));
        }

        return Result<IReadOnlyList<LineaNotaCreditoVentaBorradorResponse>>.Exito(
            await readRepository.ListLineasAsync(request.NotaCreditoVentaBorradorId, cancellationToken));
    }
}

public sealed class GetLineaNotaCreditoVentaBorradorByIdQueryHandler(INotaCreditoVentaBorradorReadRepository readRepository)
    : IRequestHandler<GetLineaNotaCreditoVentaBorradorByIdQuery, Result<LineaNotaCreditoVentaBorradorResponse>>
{
    public async Task<Result<LineaNotaCreditoVentaBorradorResponse>> Handle(
        GetLineaNotaCreditoVentaBorradorByIdQuery request, CancellationToken cancellationToken)
    {
        var item = await readRepository.GetLineaByIdAsync(request.Id, cancellationToken);
        return item is null
            ? Result<LineaNotaCreditoVentaBorradorResponse>.Fallo(NotaCreditoVentaErrores.LineaNoEncontrada())
            : Result<LineaNotaCreditoVentaBorradorResponse>.Exito(item);
    }
}

public sealed class ListLineasAcreditablesNotaCreditoVentaQueryHandler(INotaCreditoVentaBorradorReadRepository readRepository)
    : IRequestHandler<ListLineasAcreditablesNotaCreditoVentaQuery, Result<IReadOnlyList<LineaFacturaAcreditableResponse>>>
{
    public async Task<Result<IReadOnlyList<LineaFacturaAcreditableResponse>>> Handle(
        ListLineasAcreditablesNotaCreditoVentaQuery request, CancellationToken cancellationToken)
    {
        if (await readRepository.GetByIdAsync(request.NotaCreditoVentaBorradorId, cancellationToken) is null)
        {
            return Result<IReadOnlyList<LineaFacturaAcreditableResponse>>.Fallo(
                NotaCreditoVentaErrores.BorradorNoEncontrado("NotaCreditoVentaBorradorId"));
        }

        return Result<IReadOnlyList<LineaFacturaAcreditableResponse>>.Exito(
            await readRepository.ListLineasAcreditablesAsync(request.NotaCreditoVentaBorradorId, cancellationToken));
    }
}

/// <summary>
/// Vista previa de totales con los MISMOS importes que tendría el documento posteado ahora (Ruling FI): recalculados desde la factura
/// y topados por lo acreditado por notas posteadas (<see cref="TopesNotaCredito"/>), con el IVA agrupado y topado. Sin bloqueos: si
/// otra nota se postea antes, el posteo recalcula con lo vigente.
/// </summary>
public sealed class GetTotalesNotaCreditoVentaBorradorQueryHandler(INotaCreditoVentaBorradorReadRepository readRepository, INotaCreditoVentaDatos datos)
    : IRequestHandler<GetTotalesNotaCreditoVentaBorradorQuery, Result<TotalesFactura>>
{
    public async Task<Result<TotalesFactura>> Handle(GetTotalesNotaCreditoVentaBorradorQuery request, CancellationToken cancellationToken)
    {
        var borrador = await readRepository.GetByIdAsync(request.NotaCreditoVentaBorradorId, cancellationToken);
        if (borrador is null)
        {
            return Result<TotalesFactura>.Fallo(NotaCreditoVentaErrores.BorradorNoEncontrado("NotaCreditoVentaBorradorId"));
        }

        var lineas = await readRepository.ListLineasAsync(request.NotaCreditoVentaBorradorId, cancellationToken);
        var lineasFactura = (await datos.LineasFacturaAsync(borrador.FacturaVentaNumero, cancellationToken)).ToDictionary(l => l.Id);
        var acreditado = await datos.AcreditadoPorLineaAsync(borrador.FacturaVentaNumero, cancellationToken);
        var ivaFactura = await datos.IvaFacturaAsync(borrador.FacturaVentaNumero, cancellationToken);
        var aPostear = lineas.Select(l =>
        {
            var linea = new LineaNotaAPostear(
                l.Id, l.LineaFacturaVentaId, l.NumeroLinea, l.Tipo, l.ProductoId, l.CuentaContableId, l.Descripcion, l.AlmacenId,
                l.UnidadMedidaId, l.CantidadPorUnidadMedida, l.Cantidad, l.PrecioUnitario, l.PorcentajeDescuentoLinea, l.ImporteDescuentoLinea,
                l.ImporteLinea, l.GrupoProductoId, l.GrupoIvaProductoId, l.GrupoInventarioId, l.IdentificadorIva ?? string.Empty,
                l.PorcentajeIva, l.DevolverInventario);
            return lineasFactura.TryGetValue(l.LineaFacturaVentaId, out var o) ? TopesNotaCredito.DesdeOriginal(linea, o) : linea;
        }).ToList();

        try
        {
            return Result<TotalesFactura>.Exito(TopesNotaCredito.Calcular(aPostear, lineasFactura, acreditado, ivaFactura).Totales);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Result<TotalesFactura>.Fallo(new Error("nota_credito.iva_inconsistente", ex.Message, "Lineas"));
        }
    }
}
