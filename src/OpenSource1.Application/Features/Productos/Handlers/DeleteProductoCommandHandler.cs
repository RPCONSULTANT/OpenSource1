using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Productos.Commands;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Inventario;

namespace OpenSource1.Application.Features.Productos.Handlers;

/// <summary>Borrado lógico de un producto. Mismo patrón de guarda que <c>DeleteAlmacenCommandHandler</c>.</summary>
public sealed class DeleteProductoCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<DeleteProductoCommand, Result>
{
    public async Task<Result> Handle(DeleteProductoCommand request, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.Repository<Producto>();
        var entity = await repo.GetByIdAsync(new object[] { request.Id }, cancellationToken);
        if (entity is null)
        {
            return Result.Fallo(new Error(
                "producto.no_encontrado", "No se encontró el producto solicitado.", "Id"));
        }

        // El libro de inventario (append-only, sin FK-cascade de borrado) impide que un producto con movimientos
        // desaparezca aunque el borrado sea lógico: mismo motivo y mismo mensaje que DeleteAlmacenCommandHandler.
        var tieneMovimientos = await unitOfWork.Repository<MovimientoProducto>()
            .FirstOrDefaultAsync(x => x.ProductoId == entity.Id, cancellationToken: cancellationToken);
        if (tieneMovimientos is not null)
        {
            return Result.Fallo(new Error(
                "producto.conflicto",
                "No se puede eliminar el producto porque tiene movimientos de inventario registrados.",
                "Id"));
        }

        repo.Remove(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Exito();
    }
}
