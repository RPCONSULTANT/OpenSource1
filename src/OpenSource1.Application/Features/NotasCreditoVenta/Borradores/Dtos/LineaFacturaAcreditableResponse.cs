using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Dtos;

/// <summary>
/// Línea de la factura de un borrador que se puede acreditar (Producto o CuentaContable), con lo facturado, lo ya acreditado por
/// notas POSTEADAS (sin la propia nota de un borrador Posteada), lo pendiente y, si el borrador ya la incluye, el Id y la cantidad de
/// su línea de nota.
/// </summary>
public sealed class LineaFacturaAcreditableResponse
{
    public long LineaFacturaVentaId { get; init; }
    public int NumeroLinea { get; init; }
    public TipoLineaFactura Tipo { get; init; }
    public Guid? ProductoId { get; init; }
    public string? ProductoCodigo { get; init; }
    public Guid? CuentaContableId { get; init; }
    public string? CuentaContableNumero { get; init; }
    public string? Descripcion { get; init; }
    public string? UnidadMedidaCodigo { get; init; }
    public decimal PrecioUnitario { get; init; }
    public decimal PorcentajeDescuentoLinea { get; init; }
    public decimal CantidadFacturada { get; init; }
    public decimal CantidadAcreditada { get; init; }
    public decimal CantidadPendiente { get; init; }
    public Guid? LineaNotaId { get; init; }
    public decimal? CantidadEnBorrador { get; init; }
}
