using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.FacturasVenta.Borradores.Commands;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Ventas;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.FacturasVenta.Borradores.Handlers;

/// <summary>Borrado lógico de una línea bajo el <c>FOR UPDATE</c> del borrador; liberado -&gt; 400.</summary>
public sealed class DeleteLineaFacturaVentaBorradorCommandHandler(IUnitOfWork unitOfWork, IFacturaVentaBorradorBloqueoService bloqueo)
    : IRequestHandler<DeleteLineaFacturaVentaBorradorCommand, Result>
{
    public async Task<Result> Handle(DeleteLineaFacturaVentaBorradorCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<LineaFacturaVentaBorrador>();
        var entity = await repository.GetByIdAsync([request.Id], cancellationToken);
        if (entity is null)
        {
            return Result.Fallo(FacturaVentaBorradorErrores.LineaNoEncontrada());
        }

        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var estado = await bloqueo.BloquearYObtenerEstadoAsync(entity.FacturaVentaBorradorId, cancellationToken);
        if (estado is null)
        {
            return Result.Fallo(FacturaVentaBorradorErrores.LineaNoEncontrada());
        }

        if (estado == EstadoFacturaBorrador.Liberada)
        {
            return Result.Fallo(FacturaVentaBorradorErrores.Liberada("FacturaVentaBorradorId"));
        }

        repository.Remove(entity);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Exito();
    }
}
