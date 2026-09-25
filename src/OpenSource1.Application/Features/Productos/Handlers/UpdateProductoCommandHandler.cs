using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Productos.Commands;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Inventario;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.Productos.Handlers;

/// <summary>
/// Modificación de un producto con semántica parcial (ver <see cref="UpdateProductoCommand"/>): los campos no informados se
/// resuelven contra los valores guardados ANTES de validar, de modo que un PUT que solo renombra no toca precio, categoría,
/// unidad, costeo ni bloqueo. <c>CostoUnitario</c> y <c>CostoAjustado</c> nunca se tocan aquí (los mantiene el sistema); la
/// existencia (Task 3.6) tampoco: se consulta al libro solo para devolverla en la respuesta.
/// </summary>
/// <remarks>
/// Todo el handler va en una transacción: si cambia la unidad base, toma el bloqueo del producto del libro
/// (<see cref="IRegistroMovimientosInventario.BloquearProductosAsync"/>) ANTES de comprobar si tiene movimientos (Task 4.3,
/// pendiente de la Fase 3), para que un registro concurrente no escriba movimientos con la unidad base anterior.
/// </remarks>
public sealed class UpdateProductoCommandHandler(
    IUnitOfWork unitOfWork, IConsultaInventario consultaInventario, IRegistroMovimientosInventario registroMovimientos)
    : IRequestHandler<UpdateProductoCommand, Result<ProductoResponse>>
{
    public async Task<Result<ProductoResponse>> Handle(UpdateProductoCommand request, CancellationToken cancellationToken)
    {
        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);

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

        // Corrección 1 (IMPORTANT 3): la unidad base es el factor con el que el libro interpreta TODAS las cantidades ya
        // registradas del producto (Cantidad, CantidadPorUnidadMedida...); cambiarla en silencio reinterpretaría de golpe
        // el historial completo. Si cambia y el producto ya tiene algún movimiento, se rechaza con 409 sin tocar nada.
        if (datos.UnidadMedidaBaseId != entity.UnidadMedidaBaseId)
        {
            await registroMovimientos.BloquearProductosAsync([entity.Id], cancellationToken);
            var tieneMovimientos = await unitOfWork.Repository<MovimientoProducto>()
                .FirstOrDefaultAsync(x => x.ProductoId == entity.Id, cancellationToken: cancellationToken);
            if (tieneMovimientos is not null)
            {
                return Result<ProductoResponse>.Fallo(new Error(
                    "producto.conflicto",
                    "La unidad base no puede cambiarse cuando el producto tiene movimientos de inventario.",
                    "UnidadMedidaBaseId"));
            }
        }

        var imagen = await ProductoReglas.AsegurarImagenNoAsignadaAsync(unitOfWork, datos.ImagePath, request.Id, cancellationToken);
        if (imagen is not null)
        {
            return Result<ProductoResponse>.Fallo(imagen.Value);
        }

        entity.Codigo = datos.Codigo.Trim();
        entity.Nombre = datos.Nombre.Trim();
        entity.PrecioVenta = datos.PrecioVenta;
        entity.CategoriaId = datos.CategoriaId;
        entity.UnidadMedidaBaseId = datos.UnidadMedidaBaseId;
        entity.MetodoCosteo = datos.MetodoCosteo;
        entity.CostoEstandar = datos.CostoEstandar;
        entity.Bloqueado = datos.Bloqueado;
        entity.ImagePath = datos.ImagePath;

        repo.Update(entity);
        await unitOfWork.CommitAsync(cancellationToken);

        var existencia = await consultaInventario.ExistenciaAsync(entity.Id, almacenId: null, fecha: null, cancellationToken);
        return Result<ProductoResponse>.Exito(CreateProductoCommandHandler.ToResponse(entity, categoria, unidad, existencia));
    }

    /// <summary>Comando + valores guardados = datos finales del producto tras la modificación.</summary>
    private sealed record DatosResueltos(
        string Codigo,
        string Nombre,
        decimal PrecioVenta,
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
            // Guid.Empty no es una referencia válida: se rechaza en la comprobación de existencia (no significa "conservar").
            c.CategoriaId ?? actual.CategoriaId,
            c.UnidadMedidaBaseId ?? actual.UnidadMedidaBaseId,
            c.MetodoCosteo ?? actual.MetodoCosteo,
            c.CostoEstandar ?? actual.CostoEstandar,
            c.Bloqueado ?? actual.Bloqueado,
            c.ImagePath);
    }
}
