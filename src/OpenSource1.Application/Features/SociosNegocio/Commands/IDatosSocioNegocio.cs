using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.SociosNegocio.Commands;

/// <summary>
/// Datos editables de un socio de negocio, comunes a Create y Update. Excluye a propósito
/// <c>Codigo</c>: lo asigna el sistema en el alta (serie <c>SOCIOS</c>) y no se puede modificar.
/// </summary>
public interface IDatosSocioNegocio
{
    string NombreComercial { get; }
    string? RazonSocial { get; }
    TipoSocioNegocio Tipo { get; }
    TipoDocumentoFiscal TipoDocumentoFiscal { get; }
    string? NumeroDocumentoFiscal { get; }
    string? Email { get; }
    string? Telefono { get; }
    string? DireccionLinea1 { get; }
    string? DireccionLinea2 { get; }
    string? Ciudad { get; }
    string? Sector { get; }
    string? PaisCodigo { get; }
    Guid? TerminoPagoId { get; }
    decimal LimiteCredito { get; }
    BloqueoSocioNegocio Bloqueado { get; }
    string? ImagePath { get; }
}
