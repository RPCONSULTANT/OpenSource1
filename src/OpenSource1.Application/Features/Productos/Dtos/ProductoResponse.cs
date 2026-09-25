using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.Productos.Dtos;

public sealed class ProductoResponse
{
    public Guid Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public decimal PrecioVenta { get; init; }

    /// <summary>
    /// Existencia derivada del libro de inventario (Task 3.6): suma de <c>MovimientosProducto.Cantidad</c> en TODOS los
    /// almacenes, a hoy. Sustituye al antiguo <c>Stock</c> (columna eliminada); ya no se guarda en el maestro.
    /// </summary>
    public decimal Existencia { get; init; }

    public Guid CategoriaId { get; init; }
    public string CategoriaCodigo { get; init; } = string.Empty;
    public string CategoriaNombre { get; init; } = string.Empty;
    public Guid UnidadMedidaBaseId { get; init; }
    public string UnidadMedidaCodigo { get; init; } = string.Empty;
    public string UnidadMedidaNombre { get; init; } = string.Empty;

    /// <summary>Decimales de la unidad base (0-6): formato de <see cref="Existencia"/> en la UI, sin redondearla.</summary>
    public short UnidadMedidaBaseDecimales { get; init; }
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
