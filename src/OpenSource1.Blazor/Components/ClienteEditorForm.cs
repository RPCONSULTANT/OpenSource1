using System.ComponentModel.DataAnnotations;

namespace OpenSource1.Blazor.Components;

public sealed class ClienteEditorForm
{
    [Required(ErrorMessage = "El nombre comercial es obligatorio.")]
    [MaxLength(200, ErrorMessage = "Máximo 200 caracteres.")]
    public string NombreComercial { get; set; } = string.Empty;

    // Opcional desde la Task 2.6 (la API ya no lo exige); si se indica, debe tener formato válido.
    // El binder de formularios SSR entrega "" para un campo vacío y EmailAddressAttribute rechaza ""
    // (solo acepta null), así que el setter normaliza vacío/espacios a null.
    private string? _email;

    [EmailAddress(ErrorMessage = "Ingrese un email válido.")]
    [MaxLength(256, ErrorMessage = "Máximo 256 caracteres.")]
    public string? Email
    {
        get => _email;
        set => _email = string.IsNullOrWhiteSpace(value) ? null : value;
    }

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
