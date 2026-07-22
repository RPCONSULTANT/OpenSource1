namespace OpenSource1.Application.Features.Clientes.Dtos;

public sealed class ClienteResponse
{
    public Guid Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Apellido { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Telefono { get; init; }
    public string? DireccionLinea1 { get; init; }
    public string? DireccionLinea2 { get; init; }
    public string? Sector { get; init; }
    public string? PaisCodigo { get; init; }
    public string? PaisNombre { get; init; }
    public string? ImagePath { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
}
