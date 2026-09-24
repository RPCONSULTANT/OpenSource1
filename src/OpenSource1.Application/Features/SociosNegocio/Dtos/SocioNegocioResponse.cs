using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.SociosNegocio.Dtos;

/// <summary>
/// DTO de lectura del socio de negocio. Usa propiedades <c>init</c> (no un record posicional)
/// para que Dapper pueda poblarlo. Los enums se serializan como número (su valor de
/// <c>smallint</c>).
/// </summary>
public sealed class SocioNegocioResponse
{
    public Guid Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public TipoSocioNegocio Tipo { get; init; }
    public string NombreComercial { get; init; } = string.Empty;
    public string? RazonSocial { get; init; }
    public TipoDocumentoFiscal TipoDocumentoFiscal { get; init; }
    public string? NumeroDocumentoFiscal { get; init; }
    public string? Email { get; init; }
    public string? Telefono { get; init; }
    public string? DireccionLinea1 { get; init; }
    public string? DireccionLinea2 { get; init; }
    public string? Ciudad { get; init; }
    public string? Sector { get; init; }
    public string? PaisCodigo { get; init; }
    public string? PaisNombre { get; init; }
    public Guid? TerminoPagoId { get; init; }
    public decimal LimiteCredito { get; init; }
    public BloqueoSocioNegocio Bloqueado { get; init; }
    public string? ImagePath { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
}
