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

    [Required(ErrorMessage = "La categoría es obligatoria.")]
    [MaxLength(100, ErrorMessage = "Máximo 100 caracteres.")]
    public string Categoria { get; set; } = string.Empty;

    public string? ImagePath { get; set; }
}
