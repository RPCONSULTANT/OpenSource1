namespace OpenSource1.Core.Entities;

/// <summary>
/// Categoría de producto administrable (catálogo) con jerarquía opcional. <see cref="Producto"/> la
/// referencia por <c>CategoriaId</c>.
/// </summary>
public sealed class CategoriaProducto : BaseEntity
{
    public required string Codigo { get; set; }
    public required string Nombre { get; set; }

    /// <summary>Categoría padre (autorreferencial); <c>null</c> si es una categoría raíz.</summary>
    public Guid? CategoriaPadreId { get; set; }
}
