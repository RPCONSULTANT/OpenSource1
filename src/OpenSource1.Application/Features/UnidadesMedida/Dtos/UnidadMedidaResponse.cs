namespace OpenSource1.Application.Features.UnidadesMedida.Dtos;

/// <summary>
/// DTO de lectura para UnidadMedida. Usa propiedades init (no record posicional)
/// para que Dapper pueda poblar la instancia sin requerir coincidencia exacta
/// del constructor con los tipos del DataReader de Npgsql.
/// </summary>
public sealed class UnidadMedidaResponse
{
    public Guid Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public short Decimales { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
}
