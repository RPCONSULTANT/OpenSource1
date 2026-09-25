using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Almacenes.Commands;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Inventario;

namespace OpenSource1.Application.Features.Almacenes.Handlers;

/// <summary>Borrado lógico de un almacén.</summary>
public sealed class DeleteAlmacenCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteAlmacenCommand, Result>
{
    public async Task<Result> Handle(DeleteAlmacenCommand request, CancellationToken cancellationToken)
    {
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
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Exito();
    }
}
