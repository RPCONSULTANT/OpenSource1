using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.FacturasVenta.Posteadas.Dtos;

/// <summary>
/// Cabecera de una factura de venta posteada (documento legal inmutable: sin <c>Xmin</c> ni campos de modificación), con los
/// códigos/nombres ACTUALES de socios, término y almacén resueltos por JOIN y el número del asiento contable.
/// </summary>
public sealed class FacturaVentaResponse
{
    public string Numero { get; init; } = string.Empty;
    public string NumeroBorrador { get; init; } = string.Empty;

    /// <summary>Borrador <c>Posteada</c> de origen; <see langword="null"/> en facturas anteriores a no-series (su borrador se borró).</summary>
    public Guid? FacturaVentaBorradorId { get; init; }
    public Guid SocioNegocioId { get; init; }
    public string? SocioNegocioCodigo { get; init; }
    public string? SocioNegocioNombre { get; init; }
    public Guid SocioNegocioFacturarAId { get; init; }
    public string? SocioNegocioFacturarACodigo { get; init; }
    public string NombreFacturacion { get; init; } = string.Empty;
    public string? RazonSocialFacturacion { get; init; }
    public TipoDocumentoFiscal TipoDocumentoFiscal { get; init; }
    public string? NumeroDocumentoFiscal { get; init; }
    public string? DireccionFacturacionLinea1 { get; init; }
    public string? DireccionFacturacionLinea2 { get; init; }
    public string? CiudadFacturacion { get; init; }
    public string? PaisCodigoFacturacion { get; init; }
    public DateOnly FechaRegistro { get; init; }
    public DateOnly FechaDocumento { get; init; }
    public DateOnly FechaVencimiento { get; init; }
    public Guid? TerminoPagoId { get; init; }
    public string? TerminoPagoCodigo { get; init; }
    public Guid GrupoNegocioId { get; init; }
    public Guid GrupoIvaNegocioId { get; init; }
    public Guid GrupoClienteContableId { get; init; }
    public Guid AlmacenId { get; init; }
    public string? AlmacenCodigo { get; init; }
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
