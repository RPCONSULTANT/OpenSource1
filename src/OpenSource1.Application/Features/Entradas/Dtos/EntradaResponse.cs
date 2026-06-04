namespace OpenSource1.Application.Features.Entradas.Dtos;

/// <summary>
/// DTO de lectura para Entrada. Usa propiedades init (no record posicional)
/// para que Dapper pueda poblar la instancia sin requerir coincidencia exacta
/// del constructor con los tipos del DataReader de Npgsql.
/// </summary>
public sealed class EntradaResponse
{
    public Guid Id { get; init; }
    public string Titulo { get; init; } = string.Empty;
    public string? Descripcion { get; init; }
    public string Tipo { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}
