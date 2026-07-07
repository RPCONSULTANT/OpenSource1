namespace OpenSource1.Core.Entities;

public sealed class Cliente : BaseEntity
{
    public required string Nombre { get; set; }
    public required string Apellido { get; set; }
    public required string Email { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public string? ImagePath { get; set; }
}
