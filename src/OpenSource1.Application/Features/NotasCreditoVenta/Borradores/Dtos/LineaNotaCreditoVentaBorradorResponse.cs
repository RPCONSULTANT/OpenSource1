using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Dtos;

/// <summary>
/// Línea de un borrador de nota de crédito con los valores copiados de la factura, los códigos resueltos por JOIN y, de la línea de
/// la factura, lo facturado y lo ya acreditado por notas posteadas (sin la propia nota de un borrador Posteada).
/// </summary>
public sealed class LineaNotaCreditoVentaBorradorResponse
{
    public Guid Id { get; init; }
    public Guid NotaCreditoVentaBorradorId { get; init; }
    public long LineaFacturaVentaId { get; init; }
    public int NumeroLinea { get; init; }
    public TipoLineaFactura Tipo { get; init; }
    public Guid? ProductoId { get; init; }
    public string? ProductoCodigo { get; init; }
    public Guid? CuentaContableId { get; init; }
    public string? CuentaContableNumero { get; init; }
    public string? Descripcion { get; init; }
    public Guid? AlmacenId { get; init; }
    public string? AlmacenCodigo { get; init; }
    public Guid? UnidadMedidaId { get; init; }
    public string? UnidadMedidaCodigo { get; init; }
    public decimal CantidadPorUnidadMedida { get; init; }
    public decimal Cantidad { get; init; }
    public decimal CantidadFacturada { get; init; }
    public decimal CantidadAcreditada { get; init; }
    public decimal PrecioUnitario { get; init; }
    public decimal PorcentajeDescuentoLinea { get; init; }
    public decimal ImporteDescuentoLinea { get; init; }
    public decimal ImporteLinea { get; init; }
    public Guid? GrupoProductoId { get; init; }
    public Guid? GrupoIvaProductoId { get; init; }
    public Guid? GrupoInventarioId { get; init; }
    public string? IdentificadorIva { get; init; }
    public decimal PorcentajeIva { get; init; }
    public bool DevolverInventario { get; init; }
    public long Xmin { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
}
