namespace OpenSource1.Application.Features.Almacenes.Dtos;

/// <summary>
/// DTO de lectura para Almacen. Usa propiedades init (no record posicional) para que Dapper
/// pueda poblar la instancia sin requerir coincidencia exacta del constructor con los tipos del
/// DataReader de Npgsql.
/// </summary>
public sealed class AlmacenResponse
{
    public Guid Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string? DireccionLinea1 { get; init; }
    public string? DireccionLinea2 { get; init; }
    public string? Ciudad { get; init; }
    public string? PaisCodigo { get; init; }
    public bool Bloqueado { get; init; }
    public bool EsPredeterminado { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
}
