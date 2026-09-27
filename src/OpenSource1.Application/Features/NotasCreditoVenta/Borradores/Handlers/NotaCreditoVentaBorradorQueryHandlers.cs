using MediatR;
using OpenSource1.Application.Features.FacturasVenta.Calculo;
using OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Dtos;
using OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Queries;
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

/// <summary>Vista previa de totales: IVA agrupado (<see cref="CalculadoraIvaFactura"/>) sobre las líneas del borrador.</summary>
public sealed class GetTotalesNotaCreditoVentaBorradorQueryHandler(INotaCreditoVentaBorradorReadRepository readRepository)
    : IRequestHandler<GetTotalesNotaCreditoVentaBorradorQuery, Result<TotalesFactura>>
{
    public async Task<Result<TotalesFactura>> Handle(GetTotalesNotaCreditoVentaBorradorQuery request, CancellationToken cancellationToken)
    {
        if (await readRepository.GetByIdAsync(request.NotaCreditoVentaBorradorId, cancellationToken) is null)
        {
            return Result<TotalesFactura>.Fallo(NotaCreditoVentaErrores.BorradorNoEncontrado("NotaCreditoVentaBorradorId"));
        }

        var lineas = await readRepository.ListLineasAsync(request.NotaCreditoVentaBorradorId, cancellationToken);
        try
        {
            return Result<TotalesFactura>.Exito(CalculadoraIvaFactura.Calcular(
            [
                .. lineas.Select(l => new LineaCalculoIva(l.NumeroLinea, l.IdentificadorIva ?? string.Empty, l.PorcentajeIva, l.ImporteLinea))
            ]));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Result<TotalesFactura>.Fallo(new Error("nota_credito.iva_inconsistente", ex.Message, "Lineas"));
        }
    }
}
