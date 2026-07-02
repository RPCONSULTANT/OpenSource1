using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Productos.Commands;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.Productos.Handlers;

public sealed class CreateProductoCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<CreateProductoCommand, ProductoResponse>
{
    public async Task<ProductoResponse> Handle(CreateProductoCommand request, CancellationToken cancellationToken)
    {
        var entity = new Producto { Codigo = request.Codigo.Trim(), Nombre = request.Nombre.Trim(), Descripcion = request.Descripcion?.Trim(), Precio = request.Precio, Stock = request.Stock, Activo = request.Activo };
        await unitOfWork.Repository<Producto>().AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(entity);
    }

    public static ProductoResponse ToResponse(Producto x) => new() { Id = x.Id, Codigo = x.Codigo, Nombre = x.Nombre, Descripcion = x.Descripcion, Precio = x.Precio, Stock = x.Stock, Activo = x.Activo, CreatedAtUtc = x.CreatedAtUtc.UtcDateTime, UpdatedAtUtc = x.UpdatedAtUtc?.UtcDateTime };
}
