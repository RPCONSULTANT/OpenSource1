namespace OpenSource1.Application.Features.AppSettings.Dtos;

/// <summary>
/// DTO de lectura para AppSetting. Usa propiedades init (no record posicional)
/// para que Dapper pueda poblar la instancia sin requerir coincidencia exacta
/// del constructor con los tipos del DataReader de Npgsql.
/// </summary>
public sealed class AppSettingResponse
{
    public Guid Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}
