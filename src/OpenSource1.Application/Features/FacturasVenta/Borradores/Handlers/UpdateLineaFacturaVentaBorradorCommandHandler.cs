using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.FacturasVenta.Borradores.Commands;
using OpenSource1.Application.Features.FacturasVenta.Borradores.Dtos;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Ventas;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.FacturasVenta.Borradores.Handlers;

/// <summary>
/// Modificación de una línea (reemplazo completo). Línea inexistente -&gt; 404. Toma el <c>FOR UPDATE</c> del borrador antes de
/// validar (orden de locks borrador -&gt; línea, igual que los diarios); liberado -&gt; 400. <c>Xmin</c> desactualizado -&gt; 409.
/// </summary>
public sealed class UpdateLineaFacturaVentaBorradorCommandHandler(
    IUnitOfWork unitOfWork,
    IFacturaVentaBorradorBloqueoService bloqueo,
    IConversionUnidadMedidaService conversion,
    IDerivadorCuentas derivador,
    ILineaFacturaVentaBorradorReadRepository readRepository)
    : IRequestHandler<UpdateLineaFacturaVentaBorradorCommand, Result<LineaFacturaVentaBorradorResponse>>
{
    public async Task<Result<LineaFacturaVentaBorradorResponse>> Handle(
        UpdateLineaFacturaVentaBorradorCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<LineaFacturaVentaBorrador>();
        var entity = await repository.FirstOrDefaultAsync(x => x.Id == request.Id, asTracking: true, cancellationToken: cancellationToken);
        if (entity is null)
        {
            return Result<LineaFacturaVentaBorradorResponse>.Fallo(FacturaVentaBorradorErrores.LineaNoEncontrada());
        }

        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var estado = await bloqueo.BloquearYObtenerEstadoAsync(entity.FacturaVentaBorradorId, cancellationToken);
        if (estado is null)
        {
            // Imposible en operación normal: el borrado del borrador borra sus líneas bajo el mismo lock. Defensivo.
            return Result<LineaFacturaVentaBorradorResponse>.Fallo(FacturaVentaBorradorErrores.LineaNoEncontrada());
        }

        if (estado == EstadoFacturaBorrador.Posteada)
        {
            return Result<LineaFacturaVentaBorradorResponse>.Fallo(FacturaVentaBorradorErrores.Posteada("FacturaVentaBorradorId"));
        }

        if (estado == EstadoFacturaBorrador.Liberada)
        {
            return Result<LineaFacturaVentaBorradorResponse>.Fallo(FacturaVentaBorradorErrores.Liberada("FacturaVentaBorradorId"));
        }

        var cabecera = await unitOfWork.Repository<FacturaVentaBorrador>().FirstOrDefaultAsync(
            x => x.Id == entity.FacturaVentaBorradorId, cancellationToken: cancellationToken);

        var datos = new LineaFacturaDatos(
            request.Tipo, request.ProductoId, request.CuentaContableId, request.Descripcion, request.AlmacenId, request.UnidadMedidaId,
            request.Cantidad, request.PrecioUnitario, request.PorcentajeDescuentoLinea, request.GrupoIvaProductoId);
        var calculo = await LineaFacturaVentaBorradorReglas.ValidarYCalcularAsync(
            unitOfWork, conversion, derivador, cabecera!, datos, cancellationToken);
        if (!calculo.TryObtenerValor(out var valores))
        {
            return Result<LineaFacturaVentaBorradorResponse>.Fallo(calculo);
        }

        repository.EstablecerVersionOriginal(entity, request.Xmin);
        valores.Aplicar(entity);
        repository.Update(entity);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result<LineaFacturaVentaBorradorResponse>.Exito((await readRepository.GetByIdAsync(entity.Id, cancellationToken))!);
    }
}
