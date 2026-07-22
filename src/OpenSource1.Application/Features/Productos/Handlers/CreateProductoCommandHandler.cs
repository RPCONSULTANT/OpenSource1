using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Productos.Commands;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Core.Entities;
using OpenSource1.Core.ValueObjects;

namespace OpenSource1.Application.Features.Productos.Handlers;

public sealed class CreateProductoCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<CreateProductoCommand, ProductoResponse>
{
    public async Task<ProductoResponse> Handle(CreateProductoCommand request, CancellationToken cancellationToken)
    {
        var entity = new Producto
        {
            Codigo = request.Codigo.Trim(),
            Nombre = request.Nombre.Trim(),
            Precio = request.Precio,
            Stock = request.Stock,
            Categoria = new CategoriaProducto(request.CategoriaCodigo, request.CategoriaNombre),
            UnidadMedida = UnidadMedida.Of(request.UnidadMedidaCodigo),
            ImagePath = request.ImagePath
        };
        await unitOfWork.Repository<Producto>().AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(entity);
    }

    public static ProductoResponse ToResponse(Producto x) => new()
    {
        Id = x.Id,
        Codigo = x.Codigo,
        Nombre = x.Nombre,
        Precio = x.Precio,
        Stock = x.Stock,
        CategoriaCodigo = x.Categoria.Codigo,
        CategoriaNombre = x.Categoria.Nombre,
        UnidadMedidaCodigo = x.UnidadMedida.Codigo,
        UnidadMedidaNombre = x.UnidadMedida.Nombre,
        ImagePath = x.ImagePath,
        CreatedAtUtc = x.CreatedAtUtc.UtcDateTime,
        UpdatedAtUtc = x.UpdatedAtUtc?.UtcDateTime,
        CreatedBy = x.CreatedBy,
        UpdatedBy = x.UpdatedBy
    };
}
