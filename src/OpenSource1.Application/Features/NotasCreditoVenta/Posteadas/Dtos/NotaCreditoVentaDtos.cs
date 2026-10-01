using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.NotasCreditoVenta.Posteadas.Dtos;

/// <summary>
/// Cabecera de una nota de crédito posteada (documento legal inmutable), con los códigos ACTUALES de los socios y el número del
/// asiento (null en una nota de total 0).
/// </summary>
public sealed class NotaCreditoVentaResponse
{
    public string Numero { get; init; } = string.Empty;
    public string NumeroBorrador { get; init; } = string.Empty;

    /// <summary>Borrador (Posteada) del que salió la nota; <see langword="null"/> en notas anteriores a la spec no-series.</summary>
    public Guid? NotaCreditoVentaBorradorId { get; init; }

    public string FacturaVentaNumero { get; init; } = string.Empty;
    public Guid SocioNegocioId { get; init; }
    public string? SocioNegocioCodigo { get; init; }
    public string? SocioNegocioNombre { get; init; }
    public Guid SocioNegocioFacturarAId { get; init; }
    public string? SocioNegocioFacturarACodigo { get; init; }
    public string NombreFacturacion { get; init; } = string.Empty;
    public string? RazonSocialFacturacion { get; init; }
    public TipoDocumentoFiscal TipoDocumentoFiscal { get; init; }
    public string? NumeroDocumentoFiscal { get; init; }
    public DateOnly FechaRegistro { get; init; }
    public DateOnly FechaDocumento { get; init; }
    public Guid GrupoNegocioId { get; init; }
    public Guid GrupoIvaNegocioId { get; init; }
    public Guid GrupoClienteContableId { get; init; }
    public Guid? CuentaCxCId { get; init; }
    public string Moneda { get; init; } = string.Empty;
    public string? Descripcion { get; init; }
    public decimal ImporteSinIva { get; init; }
    public decimal ImporteIva { get; init; }
    public decimal ImporteTotal { get; init; }
    public long? RegistroContableId { get; init; }
    public string? NumeroRegistroContable { get; init; }
    public int NumeroLineas { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
}

/// <summary>Línea de una nota posteada, con los códigos resueltos por JOIN y el número de línea de la factura acreditada.</summary>
public sealed class LineaNotaCreditoVentaResponse
{
    public long Id { get; init; }
    public string NotaCreditoVentaNumero { get; init; } = string.Empty;
    public int NumeroLinea { get; init; }
    public long LineaFacturaVentaId { get; init; }
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
    public long? MovimientoProductoId { get; init; }
}

/// <summary>Línea de IVA (un grupo por identificador) de una nota posteada, con el número ACTUAL de la cuenta de IVA.</summary>
public sealed class LineaIvaNotaCreditoVentaResponse
{
    public long Id { get; init; }
    public string IdentificadorIva { get; init; } = string.Empty;
    public decimal PorcentajeIva { get; init; }
    public decimal BaseImponible { get; init; }
    public decimal ImporteIva { get; init; }
    public Guid CuentaIvaId { get; init; }
    public string? CuentaIvaNumero { get; init; }
}

/// <summary>Respuesta de <c>GET api/notas-credito-venta/{numero}</c>: cabecera, líneas y líneas de IVA.</summary>
public sealed record NotaCreditoVentaDetalleResponse(
    NotaCreditoVentaResponse Cabecera,
    IReadOnlyList<LineaNotaCreditoVentaResponse> Lineas,
    IReadOnlyList<LineaIvaNotaCreditoVentaResponse> LineasIva);
