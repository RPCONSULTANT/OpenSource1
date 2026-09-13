namespace OpenSource1.Application.Features.TerminosPago.Dtos;

/// <summary>
/// DTO de lectura para TerminoPago. Usa propiedades init (no record posicional)
/// para que Dapper pueda poblar la instancia sin requerir coincidencia exacta
/// del constructor con los tipos del DataReader de Npgsql.
/// </summary>
public sealed class TerminoPagoResponse
{
    public Guid Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public int DiasVencimiento { get; init; }
    public int DiasDescuento { get; init; }
    public decimal PorcentajeDescuento { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
}
