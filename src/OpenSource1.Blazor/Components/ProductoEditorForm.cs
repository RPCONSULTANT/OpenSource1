using System.ComponentModel.DataAnnotations;

namespace OpenSource1.Blazor.Components;

public sealed class ProductoEditorForm
{
    [Required(ErrorMessage = "El código es obligatorio.")]
    [MaxLength(50, ErrorMessage = "Máximo 50 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(200, ErrorMessage = "Máximo 200 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Range(0, 999999999, ErrorMessage = "El precio no puede ser negativo.")]
    public decimal Precio { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "El stock no puede ser negativo.")]
    public int Stock { get; set; }

    [Required(ErrorMessage = "El código de categoría es obligatorio.")]
    [MaxLength(30, ErrorMessage = "Máximo 30 caracteres.")]
    public string CategoriaCodigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre de categoría es obligatorio.")]
    [MaxLength(100, ErrorMessage = "Máximo 100 caracteres.")]
    public string CategoriaNombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "La unidad de medida es obligatoria.")]
    public string UnidadMedidaCodigo { get; set; } = "UND";

    public string? ImagePath { get; set; }
}
