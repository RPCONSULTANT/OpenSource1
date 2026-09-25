using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Almacenes.Commands;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.Almacenes.Handlers;

/// <summary>
/// Borrado lógico de un almacén. La guarda "tiene movimientos -> 409" se añade en la Task 3.3
/// junto con la tabla del libro de inventario y su test (no hay stub aquí a propósito).
/// </summary>
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

        repository.Remove(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Exito();
    }
}
