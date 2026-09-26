using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.FacturasVenta.Borradores.Dtos;

/// <summary>
/// Cabecera de un borrador de factura. Incluye códigos/nombres resueltos por JOIN (socios, término, almacén) y el conteo de
/// líneas vivas. <see cref="Xmin"/> es el token de concurrencia optimista que el cliente reenvía en el PUT.
/// </summary>
public sealed class FacturaVentaBorradorResponse
{
    public Guid Id { get; init; }
    public string Numero { get; init; } = string.Empty;
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
    public EstadoFacturaBorrador Estado { get; init; }
    public string Moneda { get; init; } = string.Empty;
    public string? Descripcion { get; init; }
    public int NumeroLineas { get; init; }
    public long Xmin { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
}
