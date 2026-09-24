namespace OpenSource1.Core.Entities;

/// <summary>
/// Categoría de producto administrable (catálogo) con jerarquía opcional. Reemplaza al value
/// object de texto libre <c>CategoriaProductoLegado</c>, que sigue existiendo hasta la Task 2.9
/// porque <see cref="Producto"/> todavía lo usa.
/// </summary>
public sealed class CategoriaProducto : BaseEntity
{
    public required string Codigo { get; set; }
    public required string Nombre { get; set; }

    /// <summary>Categoría padre (autorreferencial); <c>null</c> si es una categoría raíz.</summary>
    public Guid? CategoriaPadreId { get; set; }
}
