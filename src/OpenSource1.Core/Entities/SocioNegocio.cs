using OpenSource1.Core.Enums;
using OpenSource1.Core.ValueObjects;

namespace OpenSource1.Core.Entities;

public sealed class SocioNegocio : BaseEntity
{
    /// <summary>
    /// Código correlativo asignado por el sistema desde la serie <c>SOCIOS</c> (nunca por el
    /// cliente HTTP) e inmutable tras el alta.
    /// </summary>
    public required string Codigo { get; set; }

    public TipoSocioNegocio Tipo { get; set; } = TipoSocioNegocio.Cliente;
    public required string NombreComercial { get; set; }
    public string? RazonSocial { get; set; }
    public TipoDocumentoFiscal TipoDocumentoFiscal { get; set; } = TipoDocumentoFiscal.SinDocumento;
    public string? NumeroDocumentoFiscal { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public DireccionFiscal? Direccion { get; set; }
    public string? Ciudad { get; set; }
    public Pais? Pais { get; set; }
    public Sector? Sector { get; set; }
    public Guid? TerminoPagoId { get; set; }
    public decimal LimiteCredito { get; set; }
    public BloqueoSocioNegocio Bloqueado { get; set; } = BloqueoSocioNegocio.Ninguno;
    public string? ImagePath { get; set; }
}
