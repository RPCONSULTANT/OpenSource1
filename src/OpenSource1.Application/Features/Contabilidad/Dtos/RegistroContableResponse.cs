using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.Contabilidad.Dtos;

/// <summary>DTO de lectura de un registro contable (append-only). <see cref="Movimientos"/> y los totales se derivan del libro.</summary>
public sealed class RegistroContableResponse
{
    public long Id { get; init; }
    public string NumeroRegistro { get; init; } = string.Empty;
    public long DesdeMovimiento { get; init; }
    public long HastaMovimiento { get; init; }
    public int Movimientos { get; init; }
    public decimal TotalDebito { get; init; }
    public decimal TotalCredito { get; init; }
    public TipoOrigenMovimiento TipoOrigen { get; init; }
    public string ClaveOrigen { get; init; } = string.Empty;
    public DateTime FechaCreacion { get; init; }
    public string CreadoPor { get; init; } = string.Empty;
}
