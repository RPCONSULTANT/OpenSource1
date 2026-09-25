using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.Productos.Dtos;

public sealed class ProductoResponse
{
    public Guid Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public decimal PrecioVenta { get; init; }

    // LEGADO: se reemplaza por existencia derivada en la Fase 3 (libro de inventario)
    public int Stock { get; init; }

    public Guid CategoriaId { get; init; }
    public string CategoriaCodigo { get; init; } = string.Empty;
    public string CategoriaNombre { get; init; } = string.Empty;
    public Guid UnidadMedidaBaseId { get; init; }
    public string UnidadMedidaCodigo { get; init; } = string.Empty;
    public string UnidadMedidaNombre { get; init; } = string.Empty;
    public MetodoCosteo MetodoCosteo { get; init; } = MetodoCosteo.Promedio;
    public decimal CostoUnitario { get; init; }
    public decimal CostoEstandar { get; init; }
    public bool CostoAjustado { get; init; } = true;
    public BloqueoProducto Bloqueado { get; init; } = BloqueoProducto.Ninguno;
    public string? ImagePath { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
}
