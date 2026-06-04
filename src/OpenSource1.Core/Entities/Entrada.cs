namespace OpenSource1.Core.Entities;

public sealed class Entrada : BaseEntity
{
    public required string Titulo { get; set; }
    public string? Descripcion { get; set; }
    public required string Tipo { get; set; }
    public required string Estado { get; set; }
}
