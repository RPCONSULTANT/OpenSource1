using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.DiariosInventario.Lineas.Dtos;

/// <summary>
/// DTO de lectura de una línea de diario. Incluye los códigos/nombres de producto/almacenes/unidad
/// (JOIN en el repositorio Dapper) para que la UI futura no tenga que resolverlos aparte.
/// <see cref="Xmin"/> es el token de concurrencia optimista que el cliente reenvía en el PUT.
/// </summary>
public sealed class LineaDiarioResponse
{
    public Guid Id { get; init; }
    public Guid LoteDiarioId { get; init; }
    public int NumeroLinea { get; init; }
    public DateOnly FechaRegistro { get; init; }
    public DateOnly FechaDocumento { get; init; }
    public string? NumeroDocumento { get; init; }
    public TipoMovimientoInventario TipoMovimiento { get; init; }
    public Guid ProductoId { get; init; }
    public string? ProductoCodigo { get; init; }
    public string? ProductoNombre { get; init; }
    public Guid AlmacenId { get; init; }
    public string? AlmacenCodigo { get; init; }
    public Guid? AlmacenDestinoId { get; init; }
    public string? AlmacenDestinoCodigo { get; init; }
    public Guid UnidadMedidaId { get; init; }
    public string? UnidadMedidaCodigo { get; init; }
    public decimal CantidadPorUnidadMedida { get; init; }
    public decimal Cantidad { get; init; }
    public decimal? CostoUnitario { get; init; }
    public decimal ImporteCosto { get; init; }
    public string? Descripcion { get; init; }
    public long Xmin { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
}
