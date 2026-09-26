using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.MovimientosCliente.Dtos;

/// <summary>
/// Movimiento del libro de clientes con su importe restante DERIVADO (<c>Σ detalle.Importe</c>) y la marca de abierto
/// (<c>restante ≠ 0</c>). Append-only: sin <c>Xmin</c> ni campos de modificación.
/// </summary>
public sealed class MovimientoClienteResponse
{
    public long Id { get; init; }
    public Guid SocioNegocioId { get; init; }
    public DateOnly FechaRegistro { get; init; }
    public DateOnly FechaDocumento { get; init; }
    public DateOnly FechaVencimiento { get; init; }
    public TipoDocumentoCliente TipoDocumento { get; init; }
    public string NumeroDocumento { get; init; } = string.Empty;
    public string? Descripcion { get; init; }
    public decimal ImporteOriginal { get; init; }
    public decimal ImporteRestante { get; init; }
    public bool Abierta { get; init; }
    public Guid GrupoClienteContableId { get; init; }
    public Guid CuentaCxCId { get; init; }

    /// <summary>Número ACTUAL de la cuenta de CxC congelada.</summary>
    public string? NumeroCuentaCxC { get; init; }

    public TipoOrigenMovimiento TipoOrigen { get; init; }
    public string ClaveOrigen { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
}
