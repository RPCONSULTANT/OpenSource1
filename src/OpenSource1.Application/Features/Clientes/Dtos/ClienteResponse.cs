namespace OpenSource1.Application.Features.Clientes.Dtos;

public sealed class ClienteResponse
{
    public Guid Id { get; init; }
    public string NombreCompleto { get; init; } = string.Empty;
    public string DocumentoIdentidad { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Telefono { get; init; }
    public string? Direccion { get; init; }
    public bool Activo { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}
