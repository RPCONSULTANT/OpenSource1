using System.ComponentModel.DataAnnotations;

namespace OpenSource1.Blazor.Components;

public sealed class ClienteEditorForm
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(100, ErrorMessage = "Máximo 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [MaxLength(100, ErrorMessage = "Máximo 100 caracteres.")]
    public string Apellido { get; set; } = string.Empty;

    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingrese un email válido.")]
    [MaxLength(256, ErrorMessage = "Máximo 256 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [MaxLength(50, ErrorMessage = "Máximo 50 caracteres.")]
    public string? Telefono { get; set; }

    [MaxLength(300, ErrorMessage = "Máximo 300 caracteres.")]
    public string? DireccionLinea1 { get; set; }

    [MaxLength(300, ErrorMessage = "Máximo 300 caracteres.")]
    public string? DireccionLinea2 { get; set; }

    [MaxLength(100, ErrorMessage = "Máximo 100 caracteres.")]
    public string? Sector { get; set; }

    public string? PaisCodigo { get; set; }

    public string? ImagePath { get; set; }
}
