using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.FacturasVenta.Borradores.Commands;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Ventas;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.FacturasVenta.Borradores.Handlers;

/// <summary>
/// Borrado lógico del borrador y de todas sus líneas, bajo el <c>FOR UPDATE</c> del borrador (una línea concurrente no puede
/// quedar viva en un borrador borrado: el alta que pierde la carrera ve el borrador borrado -&gt; 404).
/// </summary>
public sealed class DeleteFacturaVentaBorradorCommandHandler(
    IUnitOfWork unitOfWork, IFacturaVentaBorradorBloqueoService bloqueo, IFacturaVentaBorradorDatos datos, IUsuarioActual usuario)
    : IRequestHandler<DeleteFacturaVentaBorradorCommand, Result>
{
    public async Task<Result> Handle(DeleteFacturaVentaBorradorCommand request, CancellationToken cancellationToken)
    {
        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var estado = await bloqueo.BloquearYObtenerEstadoAsync(request.Id, cancellationToken);
        if (estado is null)
        {
            return Result.Fallo(FacturaVentaBorradorErrores.BorradorNoEncontrado());
        }

        if (estado == EstadoFacturaBorrador.Liberada)
        {
            return Result.Fallo(FacturaVentaBorradorErrores.Liberada());
        }

        var repository = unitOfWork.Repository<FacturaVentaBorrador>();
        var entity = await repository.GetByIdAsync([request.Id], cancellationToken);
        if (entity is null)
        {
            return Result.Fallo(FacturaVentaBorradorErrores.BorradorNoEncontrado());
        }

        var nombre = string.IsNullOrWhiteSpace(usuario.Nombre) ? "system" : usuario.Nombre;
        await datos.BorrarLineasAsync(request.Id, nombre.Length <= 100 ? nombre : nombre[..100], cancellationToken);

        repository.Remove(entity);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Exito();
    }
}
