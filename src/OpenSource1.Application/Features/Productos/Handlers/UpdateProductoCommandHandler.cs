using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Productos.Commands;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.Productos.Handlers;

/// <summary>
/// Modificación de un producto con semántica parcial (ver <see cref="UpdateProductoCommand"/>): los campos no informados se
/// resuelven contra los valores guardados ANTES de validar, de modo que un PUT que solo renombra no toca precio, stock, categoría,
/// unidad, costeo ni bloqueo. <c>CostoUnitario</c> y <c>CostoAjustado</c> nunca se tocan aquí (los mantiene el sistema).
/// </summary>
public sealed class UpdateProductoCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateProductoCommand, Result<ProductoResponse>>
{
    public async Task<Result<ProductoResponse>> Handle(UpdateProductoCommand request, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.Repository<Producto>();

        // Consulta con seguimiento (no Find): siempre pasa por el filtro global de soft delete.
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == request.Id, asTracking: true, cancellationToken: cancellationToken);
        if (entity is null)
        {
            return Result<ProductoResponse>.Fallo(new Error(
                "producto.no_encontrado", "No se encontró el producto solicitado.", "Id"));
        }

        var datos = DatosResueltos.De(request, entity);

        var errores = ProductoValidator.Validar(datos);
        if (errores.Count > 0)
        {
            return Result<ProductoResponse>.Fallo([.. errores]);
        }

        // La categoría y la unidad solo se revalidan si CAMBIAN: un producto con una referencia ya colgante (dato previo a que el
        // borrado de catálogos en uso se bloqueara) debe seguir siendo editable.
        var categoria = await ProductoReglas.BuscarCategoriaAsync(unitOfWork, datos.CategoriaId, cancellationToken);
        if (categoria is null && datos.CategoriaId != entity.CategoriaId)
        {
            return Result<ProductoResponse>.Fallo(ProductoReglas.ErrorCategoria(datos.CategoriaId));
        }

        var unidad = await ProductoReglas.BuscarUnidadAsync(unitOfWork, datos.UnidadMedidaBaseId, cancellationToken);
        if (unidad is null && datos.UnidadMedidaBaseId != entity.UnidadMedidaBaseId)
        {
            return Result<ProductoResponse>.Fallo(ProductoReglas.ErrorUnidad(datos.UnidadMedidaBaseId));
        }

        var imagen = await ProductoReglas.AsegurarImagenNoAsignadaAsync(unitOfWork, datos.ImagePath, request.Id, cancellationToken);
        if (imagen is not null)
        {
            return Result<ProductoResponse>.Fallo(imagen.Value);
        }

        entity.Codigo = datos.Codigo.Trim();
        entity.Nombre = datos.Nombre.Trim();
        entity.PrecioVenta = datos.PrecioVenta;
        entity.Stock = datos.Stock;
        entity.CategoriaId = datos.CategoriaId;
        entity.UnidadMedidaBaseId = datos.UnidadMedidaBaseId;
        entity.MetodoCosteo = datos.MetodoCosteo;
        entity.CostoEstandar = datos.CostoEstandar;
        entity.Bloqueado = datos.Bloqueado;
        entity.ImagePath = datos.ImagePath;

        repo.Update(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ProductoResponse>.Exito(CreateProductoCommandHandler.ToResponse(entity, categoria, unidad));
    }

    /// <summary>Comando + valores guardados = datos finales del producto tras la modificación.</summary>
    private sealed record DatosResueltos(
        string Codigo,
        string Nombre,
        decimal PrecioVenta,
        int Stock,
        Guid CategoriaId,
        Guid UnidadMedidaBaseId,
        MetodoCosteo MetodoCosteo,
        decimal CostoEstandar,
        BloqueoProducto Bloqueado,
        string? ImagePath) : IDatosProducto
    {
        public static DatosResueltos De(UpdateProductoCommand c, Producto actual) => new(
            c.Codigo,
            c.Nombre,
            c.PrecioVenta ?? actual.PrecioVenta,
            c.Stock ?? actual.Stock,
            // Guid.Empty no es una referencia válida: se rechaza en la comprobación de existencia (no significa "conservar").
            c.CategoriaId ?? actual.CategoriaId,
            c.UnidadMedidaBaseId ?? actual.UnidadMedidaBaseId,
            c.MetodoCosteo ?? actual.MetodoCosteo,
            c.CostoEstandar ?? actual.CostoEstandar,
            c.Bloqueado ?? actual.Bloqueado,
            c.ImagePath);
    }
}
