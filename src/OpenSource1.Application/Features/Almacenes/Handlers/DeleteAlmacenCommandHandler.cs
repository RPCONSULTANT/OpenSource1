using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Almacenes.Commands;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Inventario;

namespace OpenSource1.Application.Features.Almacenes.Handlers;

/// <summary>
/// Borrado lógico de un almacén. En una transacción y con el bloqueo EXCLUSIVO del almacén tomado ANTES de comprobar si
/// tiene movimientos (Task 4.3, pendiente de la Fase 3): <c>RegistrarAsync</c> toma el mismo bloqueo en modo compartido,
/// así que un registro concurrente o termina antes (y el borrado ve su movimiento: 409) o espera y ve el almacén borrado.
/// </summary>
public sealed class DeleteAlmacenCommandHandler(IUnitOfWork unitOfWork, IRegistroMovimientosInventario registroMovimientos)
    : IRequestHandler<DeleteAlmacenCommand, Result>
{
    public async Task<Result> Handle(DeleteAlmacenCommand request, CancellationToken cancellationToken)
    {
        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await registroMovimientos.BloquearAlmacenExclusivoAsync(request.Id, cancellationToken);

        var repository = unitOfWork.Repository<Almacen>();
        var entity = await repository.GetByIdAsync([request.Id], cancellationToken);

        if (entity is null)
        {
            return Result.Fallo(new Error(
                "almacen.no_encontrado", "No se encontró el almacén solicitado.", "Id"));
        }

        if (entity.EsPredeterminado)
        {
            return Result.Fallo(new Error(
                "almacen.conflicto",
                "No se puede eliminar el almacén predeterminado; marque otro como predeterminado primero.",
                "Id"));
        }

        // El libro de inventario (Task 3.3) es append-only y no tiene FK-cascade de borrado: un
        // almacén con movimientos no puede desaparecer aunque el borrado sea lógico, porque las
        // consultas de existencia/valorización sobre él dejarían de tener sentido.
        var tieneMovimientos = await unitOfWork.Repository<MovimientoProducto>()
            .FirstOrDefaultAsync(x => x.AlmacenId == request.Id, cancellationToken: cancellationToken);
        if (tieneMovimientos is not null)
        {
            return Result.Fallo(new Error(
                "almacen.conflicto",
                "No se puede eliminar el almacén porque tiene movimientos de inventario registrados.",
                "Id"));
        }

        repository.Remove(entity);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Exito();
    }
}
