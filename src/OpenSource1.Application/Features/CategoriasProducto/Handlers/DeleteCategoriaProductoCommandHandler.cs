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

        // El borrado es lógico, así que la FK Restrict de CategoriaPadreId no lo detiene: sin esta
        // guarda quedarían subcategorías apuntando a un padre "borrado" (el filtro global de EF lo
        // oculta) y la jerarquía quedaría rota en silencio.
        //
        // LIMITACIÓN CONOCIDA: aquí solo se comprueban las subcategorías. Hoy Producto todavía
        // referencia su categoría por el value object legado (CategoriaCodigo/CategoriaNombre en
        // texto libre), no por un CategoriaId; por eso no hay forma de saber si un producto "usa"
        // esta categoría. La Task 2.9 introduce Producto.CategoriaId y debe añadir aquí la
        // comprobación de uso por productos con el mismo código de error (con_hijos.conflicto).
        var hijo = await repository.FirstOrDefaultAsync(x => x.CategoriaPadreId == request.Id, cancellationToken: cancellationToken);

        if (hijo is not null)
        {
            return Result.Fallo(new Error(
                "categoria_producto.con_hijos.conflicto",
                "No se puede eliminar la categoría porque tiene subcategorías asociadas.", "Id"));
        }

        repository.Remove(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Exito();
    }
}
