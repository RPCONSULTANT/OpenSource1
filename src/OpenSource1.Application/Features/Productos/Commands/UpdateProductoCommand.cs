using MediatR;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.Productos.Commands;

/// <summary>
/// Modificación de un producto. Los campos <c>PrecioVenta</c>, <c>CategoriaId</c>, <c>UnidadMedidaBaseId</c>,
/// <c>MetodoCosteo</c>, <c>CostoEstandar</c> y <c>Bloqueado</c> tienen semántica de MODIFICACIÓN PARCIAL para que un PUT que no
/// los menciona no los pise con valores por defecto (p. ej. desbloquear en silencio un producto bloqueado o dejar su precio en 0):
/// <c>null</c> (ausente) = conservar el valor guardado; valor informado = se aplica. El handler fusiona con lo guardado y valida el
/// RESULTADO. <c>Codigo</c>, <c>Nombre</c> e <c>ImagePath</c> son de reemplazo completo. <c>CostoUnitario</c> y <c>CostoAjustado</c>
/// no forman parte del comando (los mantiene el sistema). <c>Stock</c> ya no existe (Task 3.6): la existencia se deriva del
/// libro de inventario y se modifica con diarios de inventario (Fase 4), no con este comando.
/// </summary>
public sealed record UpdateProductoCommand(
    Guid Id,
    string Codigo,
    string Nombre,
    decimal? PrecioVenta,
    Guid? CategoriaId,
    Guid? UnidadMedidaBaseId,
    MetodoCosteo? MetodoCosteo,
    decimal? CostoEstandar,
    BloqueoProducto? Bloqueado,
    string? ImagePath = null) : IRequest<Result<ProductoResponse>>;
