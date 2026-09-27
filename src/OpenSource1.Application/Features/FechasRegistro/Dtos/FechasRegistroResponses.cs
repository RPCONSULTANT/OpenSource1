namespace OpenSource1.Application.Features.FechasRegistro.Dtos;

/// <summary>Configuración general de las fechas de registro permitidas (fila única). Límites null = sin límite.</summary>
public sealed class FechasRegistroGeneralResponse
{
    public DateOnly? PermitirRegistroDesde { get; init; }
    public DateOnly? PermitirRegistroHasta { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string? UpdatedBy { get; init; }
}

/// <summary>Excepción por usuario a las fechas de registro permitidas.</summary>
public sealed class FechasRegistroUsuarioResponse
{
    public Guid Id { get; init; }
    public Guid UsuarioId { get; init; }
    public string NombreUsuario { get; init; } = string.Empty;
    public DateOnly? PermitirRegistroDesde { get; init; }
    public DateOnly? PermitirRegistroHasta { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
}
