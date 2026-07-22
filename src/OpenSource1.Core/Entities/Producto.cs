using OpenSource1.Core.ValueObjects;

namespace OpenSource1.Core.Entities;

public sealed class Producto : BaseEntity
{
    public required string Codigo { get; set; }
    public required string Nombre { get; set; }
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public required CategoriaProducto Categoria { get; set; }
    public required UnidadMedida UnidadMedida { get; set; }
    public string? ImagePath { get; set; }
}
