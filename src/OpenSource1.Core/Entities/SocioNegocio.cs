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

    // Clasificación contable (Fase 5, Task 5.3), todas FK nulables. La migración AddGruposContables asignó
    // NACIONAL / ITBIS18 / GENERAL a los socios existentes.

    /// <summary>Grupo contable de negocio (<c>GruposNegocio</c>): eje del setup general con el grupo de producto.</summary>
    public Guid? GrupoNegocioId { get; set; }

    /// <summary>Grupo de IVA del socio (<c>GruposIvaNegocio</c>): eje del setup de IVA con el grupo de IVA del producto.</summary>
    public Guid? GrupoIvaNegocioId { get; set; }

    /// <summary>Grupo contable de cliente (<c>GruposClienteContable</c>): cuenta por cobrar del socio.</summary>
    public Guid? GrupoClienteContableId { get; set; }
}
