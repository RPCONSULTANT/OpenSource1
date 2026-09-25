using MediatR;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.Productos.Commands;

/// <summary>
/// Alta de producto. <c>CategoriaId</c> y <c>UnidadMedidaBaseId</c> son opcionales: si no vienen se usan la categoría
/// <c>GENERAL</c> y la unidad <c>UND</c> del catálogo. <c>CostoUnitario</c> nace en 0 y <c>CostoAjustado</c> en
/// <c>true</c> (no hay movimientos que ajustar): los mantiene el sistema y no forman parte del comando.
/// </summary>
public sealed record CreateProductoCommand(
    string Codigo,
    string Nombre,
    decimal PrecioVenta,
    int Stock,
    Guid? CategoriaId = null,
    Guid? UnidadMedidaBaseId = null,
    MetodoCosteo MetodoCosteo = MetodoCosteo.Promedio,
    decimal CostoEstandar = 0m,
    BloqueoProducto Bloqueado = BloqueoProducto.Ninguno,
    string? ImagePath = null) : IRequest<Result<ProductoResponse>>, IDatosProducto;
