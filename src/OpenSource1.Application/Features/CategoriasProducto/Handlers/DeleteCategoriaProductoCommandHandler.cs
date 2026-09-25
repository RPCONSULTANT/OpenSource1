using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.CategoriasProducto.Commands;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.CategoriasProducto.Handlers;

public sealed class DeleteCategoriaProductoCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteCategoriaProductoCommand, Result>
{
    public async Task<Result> Handle(DeleteCategoriaProductoCommand request, CancellationToken cancellationToken)
    {
        var repository = unitOfWork.Repository<CategoriaProducto>();
        var entity = await repository.GetByIdAsync([request.Id], cancellationToken);

        if (entity is null)
        {
            return Result.Fallo(new Error(
                "categoria_producto.no_encontrado", "No se encontró la categoría solicitada.", "Id"));
        }

        // El borrado es lógico, así que las FK Restrict no lo detienen: sin estas guardas quedarían subcategorías
        // (CategoriaPadreId) o productos (CategoriaId) apuntando a una categoría "borrada" (el filtro global de EF la
        // oculta) y la jerarquía o la clasificación quedarían rotas en silencio.
        var hijo = await repository.FirstOrDefaultAsync(x => x.CategoriaPadreId == request.Id, cancellationToken: cancellationToken);

        if (hijo is not null)
        {
            return Result.Fallo(new Error(
                "categoria_producto.con_hijos.conflicto",
                "No se puede eliminar la categoría porque tiene subcategorías asociadas.", "Id"));
        }

        var producto = await unitOfWork.Repository<Producto>()
            .FirstOrDefaultAsync(x => x.CategoriaId == request.Id, cancellationToken: cancellationToken);

        if (producto is not null)
        {
            return Result.Fallo(new Error(
                "categoria_producto.en_uso.conflicto",
                "No se puede eliminar la categoría porque tiene productos asociados.", "Id"));
        }

        repository.Remove(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Exito();
    }
}
