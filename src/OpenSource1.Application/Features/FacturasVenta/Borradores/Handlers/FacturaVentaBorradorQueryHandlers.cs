using MediatR;
using OpenSource1.Application.Features.FacturasVenta.Borradores.Dtos;
using OpenSource1.Application.Features.FacturasVenta.Borradores.Queries;
using OpenSource1.Application.Features.FacturasVenta.Calculo;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.FacturasVenta.Borradores.Handlers;

public sealed class GetFacturaVentaBorradorByIdQueryHandler(IFacturaVentaBorradorReadRepository readRepository)
    : IRequestHandler<GetFacturaVentaBorradorByIdQuery, Result<FacturaVentaBorradorResponse>>
{
    public async Task<Result<FacturaVentaBorradorResponse>> Handle(GetFacturaVentaBorradorByIdQuery request, CancellationToken cancellationToken)
    {
        var item = await readRepository.GetByIdAsync(request.Id, cancellationToken);
        return item is null
            ? Result<FacturaVentaBorradorResponse>.Fallo(FacturaVentaBorradorErrores.BorradorNoEncontrado())
            : Result<FacturaVentaBorradorResponse>.Exito(item);
    }
}

public sealed class ListFacturasVentaBorradorQueryHandler(IFacturaVentaBorradorReadRepository readRepository)
    : IRequestHandler<ListFacturasVentaBorradorQuery, Result<PagedResult<FacturaVentaBorradorResponse>>>
{
    public Task<Result<PagedResult<FacturaVentaBorradorResponse>>> Handle(ListFacturasVentaBorradorQuery request, CancellationToken cancellationToken) =>
        readRepository.ListAsync(request.Search, request.Paginacion, cancellationToken);
}

public sealed class ListLineasFacturaVentaBorradorQueryHandler(
    IFacturaVentaBorradorReadRepository borradorReadRepository, ILineaFacturaVentaBorradorReadRepository readRepository)
    : IRequestHandler<ListLineasFacturaVentaBorradorQuery, Result<IReadOnlyList<LineaFacturaVentaBorradorResponse>>>
{
    public async Task<Result<IReadOnlyList<LineaFacturaVentaBorradorResponse>>> Handle(
        ListLineasFacturaVentaBorradorQuery request, CancellationToken cancellationToken)
    {
        if (await borradorReadRepository.GetByIdAsync(request.FacturaVentaBorradorId, cancellationToken) is null)
        {
            return Result<IReadOnlyList<LineaFacturaVentaBorradorResponse>>.Fallo(
                FacturaVentaBorradorErrores.BorradorNoEncontrado("FacturaVentaBorradorId"));
        }

        return Result<IReadOnlyList<LineaFacturaVentaBorradorResponse>>.Exito(
            await readRepository.ListByBorradorAsync(request.FacturaVentaBorradorId, cancellationToken));
    }
}

public sealed class GetLineaFacturaVentaBorradorByIdQueryHandler(ILineaFacturaVentaBorradorReadRepository readRepository)
    : IRequestHandler<GetLineaFacturaVentaBorradorByIdQuery, Result<LineaFacturaVentaBorradorResponse>>
{
    public async Task<Result<LineaFacturaVentaBorradorResponse>> Handle(
        GetLineaFacturaVentaBorradorByIdQuery request, CancellationToken cancellationToken)
    {
        var item = await readRepository.GetByIdAsync(request.Id, cancellationToken);
        return item is null
            ? Result<LineaFacturaVentaBorradorResponse>.Fallo(FacturaVentaBorradorErrores.LineaNoEncontrada())
            : Result<LineaFacturaVentaBorradorResponse>.Exito(item);
    }
}

/// <summary>Vista previa de totales: IVA agrupado (<see cref="CalculadoraIvaFactura"/>) sobre las líneas que no son comentario.</summary>
public sealed class GetTotalesFacturaVentaBorradorQueryHandler(
    IFacturaVentaBorradorReadRepository borradorReadRepository, ILineaFacturaVentaBorradorReadRepository readRepository)
    : IRequestHandler<GetTotalesFacturaVentaBorradorQuery, Result<TotalesFactura>>
{
    public async Task<Result<TotalesFactura>> Handle(GetTotalesFacturaVentaBorradorQuery request, CancellationToken cancellationToken)
    {
        if (await borradorReadRepository.GetByIdAsync(request.FacturaVentaBorradorId, cancellationToken) is null)
        {
            return Result<TotalesFactura>.Fallo(FacturaVentaBorradorErrores.BorradorNoEncontrado("FacturaVentaBorradorId"));
        }

        var lineas = await readRepository.ListByBorradorAsync(request.FacturaVentaBorradorId, cancellationToken);
        try
        {
            return Result<TotalesFactura>.Exito(CalculadoraIvaFactura.Calcular(
            [
                .. lineas
                    .Where(l => l.Tipo != TipoLineaFactura.Comentario)
                    .Select(l => new LineaCalculoIva(l.NumeroLinea, l.IdentificadorIva ?? string.Empty, l.PorcentajeIva, l.ImporteLinea))
            ]));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            // Posible en un borrador si el porcentaje de un setup de IVA cambió entre el alta de dos líneas: el IVA congelado de
            // las líneas ya no es coherente. 400 legible en vez de 500; se corrige volviendo a guardar las líneas afectadas.
            // ArgumentException: una línea no comentario sin identificador de IVA (no debería ocurrir: se congela al guardarla).
            return Result<TotalesFactura>.Fallo(new Error("factura.iva_inconsistente", ex.Message, "Lineas"));
        }
    }
}
