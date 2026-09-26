using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.Contabilidad.Dtos;

/// <summary>DTO de lectura de un movimiento del libro contable (append-only: sin <c>Xmin</c> ni campos de modificación).</summary>
public sealed class MovimientoContableResponse
{
    public long Id { get; init; }
    public Guid CuentaContableId { get; init; }

    /// <summary>Número congelado al registrar.</summary>
    public string NumeroCuenta { get; init; } = string.Empty;

    /// <summary>Nombre ACTUAL de la cuenta (aunque esté borrada lógicamente).</summary>
    public string? NombreCuenta { get; init; }

    public DateOnly FechaRegistro { get; init; }
    public DateOnly FechaDocumento { get; init; }
    public TipoDocumentoContable TipoDocumento { get; init; }
    public string? NumeroDocumento { get; init; }
    public string Descripcion { get; init; } = string.Empty;
    public decimal Importe { get; init; }
    public decimal Debito { get; init; }
    public decimal Credito { get; init; }
    public long RegistroContableId { get; init; }
    public string NumeroRegistro { get; init; } = string.Empty;
    public Guid? SocioNegocioId { get; init; }
    public Guid? ProductoId { get; init; }
    public Guid? GrupoNegocioId { get; init; }
    public Guid? GrupoProductoId { get; init; }
    public Guid? GrupoIvaNegocioId { get; init; }
    public Guid? GrupoIvaProductoId { get; init; }
    public TipoOrigenMovimiento TipoOrigen { get; init; }
    public string ClaveOrigen { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
}
