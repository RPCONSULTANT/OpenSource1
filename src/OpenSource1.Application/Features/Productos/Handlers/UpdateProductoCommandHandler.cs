using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Productos.Commands;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.Productos.Handlers;

public sealed class UpdateProductoCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<UpdateProductoCommand, ProductoResponse?>
{
    public async Task<ProductoResponse?> Handle(UpdateProductoCommand request, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.Repository<Producto>();
        var entity = await repo.GetByIdAsync(new object[] { request.Id }, cancellationToken);
        if (entity is null) return null;
        entity.Codigo = request.Codigo.Trim(); entity.Nombre = request.Nombre.Trim(); entity.Descripcion = request.Descripcion?.Trim(); entity.Precio = request.Precio; entity.Stock = request.Stock; entity.Activo = request.Activo;
        repo.Update(entity); await unitOfWork.SaveChangesAsync(cancellationToken); return CreateProductoCommandHandler.ToResponse(entity);
    }
}
