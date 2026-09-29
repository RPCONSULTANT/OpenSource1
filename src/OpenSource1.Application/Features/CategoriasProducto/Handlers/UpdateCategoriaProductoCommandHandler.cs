using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.CategoriasProducto.Commands;
using OpenSource1.Application.Features.CategoriasProducto.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.CategoriasProducto.Handlers;

public sealed class UpdateCategoriaProductoCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateCategoriaProductoCommand, Result<CategoriaProductoResponse>>
{
    public async Task<Result<CategoriaProductoResponse>> Handle(UpdateCategoriaProductoCommand request, CancellationToken cancellationToken)
    {
        var errores = CategoriaProductoValidator.Validar(request.Codigo, request.Nombre);

        if (errores.Count > 0)
        {
            return Result<CategoriaProductoResponse>.Fallo([.. errores]);
        }

        var repository = unitOfWork.Repository<CategoriaProducto>();
        var entity = await repository.GetByIdAsync([request.Id], cancellationToken);

        if (entity is null)
        {
            return Result<CategoriaProductoResponse>.Fallo(new Error(
                "categoria_producto.no_encontrado", "No se encontró la categoría solicitada.", "Id"));
        }

        var (error, padre) = await CategoriaProductoJerarquia.ValidarPadreAsync(
            repository, request.Id, request.CategoriaPadreId, cancellationToken);

        if (error is not null)
        {
            return Result<CategoriaProductoResponse>.Fallo(error.Value);
        }

        entity.Codigo = CategoriaProductoValidator.NormalizarCodigo(request.Codigo);
        entity.Nombre = request.Nombre.Trim();
        entity.CategoriaPadreId = request.CategoriaPadreId;

        repository.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CategoriaProductoResponse>.Exito(CreateCategoriaProductoCommandHandler.ToResponse(entity, padre));
    }
}
