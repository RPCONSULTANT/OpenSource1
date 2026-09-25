using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Productos.Commands;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Application.Storage;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.ValueObjects;

namespace OpenSource1.Application.Features.Productos.Handlers;

public sealed class CreateProductoCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<CreateProductoCommand, ProductoResponse>
{
    public async Task<ProductoResponse> Handle(CreateProductoCommand request, CancellationToken cancellationToken)
    {
        // La ruta acaba en un borrado de fichero al sustituir la imagen: solo se acepta /uploads/productos/<nombre>.
        if (RutaImagen.Validar(request.ImagePath, RutaImagen.CarpetaProductos) is { } errorImagen)
        {
            throw new ErroresDeDominioException(errorImagen);
        }

        await AsegurarImagenNoAsignadaAsync(unitOfWork, request.ImagePath, null, cancellationToken);

        var entity = new Producto
        {
            Codigo = request.Codigo.Trim(),
            Nombre = request.Nombre.Trim(),
            Precio = request.Precio,
            Stock = request.Stock,
            Categoria = CategoriaProductoLegado.Of(request.CategoriaCodigo, request.CategoriaNombre, nameof(request.CategoriaCodigo), nameof(request.CategoriaNombre)),
            UnidadMedida = UnidadMedidaLegado.Of(request.UnidadMedidaCodigo, nameof(request.UnidadMedidaCodigo)),
            ImagePath = request.ImagePath
        };
        await unitOfWork.Repository<Producto>().AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(entity);
    }

    /// <summary>
    /// Una imagen solo puede pertenecer a UN producto (si no, sustituir la de uno borraría la de otro).
    /// LIMITACIÓN CONOCIDA: es una consulta previa SIN índice único; dos peticiones concurrentes con el mismo <c>imagePath</c> pueden
    /// asignarlo a dos registros (medido: 40 de 40 rondas). Consecuencia: al sustituir la imagen de uno se borra el fichero y el otro queda
    /// con la imagen rota; NO es explotable para borrar el fichero de una víctima ajena. Corrección de raíz recomendada (no implementada):
    /// índice único parcial sobre <c>ImagePath</c> (no nulo y no borrado) por tabla, o comprobar referencias antes de borrar el fichero.
    /// </summary>
    public static async Task AsegurarImagenNoAsignadaAsync(IUnitOfWork unitOfWork, string? imagePath, Guid? idActual, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(imagePath))
        {
            return;
        }

        var enUso = await unitOfWork.Repository<Producto>().FirstOrDefaultAsync(
            x => x.ImagePath == imagePath && (idActual == null || x.Id != idActual),
            cancellationToken: cancellationToken);
        if (enUso is not null)
        {
            throw new ErroresDeDominioException(new Error(
                "producto.imagen_en_uso", "La imagen ya está asignada a otro producto.", RutaImagen.Campo));
        }
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
