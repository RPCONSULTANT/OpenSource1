namespace OpenSource1.Core.Entities;

public sealed class Cliente : BaseEntity
{
    public required string NombreCompleto { get; set; }
    public required string DocumentoIdentidad { get; set; }
    public required string Email { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public bool Activo { get; set; } = true;
}
