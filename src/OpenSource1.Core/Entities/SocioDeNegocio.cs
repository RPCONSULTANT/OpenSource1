using OpenSource1.Core.ValueObjects;

namespace OpenSource1.Core.Entities;

public sealed class SocioDeNegocio : BaseEntity
{
    public required string Nombre { get; set; }
    public required string Apellido { get; set; }
    public required string Email { get; set; }
    public string? Telefono { get; set; }
    public DireccionFiscal? Direccion { get; set; }
    public Pais? Pais { get; set; }
    public Sector? Sector { get; set; }
    public string? ImagePath { get; set; }
}
