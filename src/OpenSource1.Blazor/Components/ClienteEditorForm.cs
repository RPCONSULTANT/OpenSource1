using System.ComponentModel.DataAnnotations;
using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Components;

public sealed class ClienteEditorForm : IValidatableObject
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

    // Campos de facturación (Task 2.7). El Codigo NO está aquí a propósito: lo asigna el sistema y la UI
    // solo lo muestra, así ningún POST puede intentar fijarlo.
    public TipoSocioNegocio Tipo { get; set; } = TipoSocioNegocio.Cliente;

    [MaxLength(200, ErrorMessage = "Máximo 200 caracteres.")]
    public string? RazonSocial { get; set; }

    public TipoDocumentoFiscal TipoDocumentoFiscal { get; set; } = TipoDocumentoFiscal.SinDocumento;

    [MaxLength(20, ErrorMessage = "Máximo 20 caracteres.")]
    public string? NumeroDocumentoFiscal { get; set; }

    [MaxLength(100, ErrorMessage = "Máximo 100 caracteres.")]
    public string? Ciudad { get; set; }

    // "" (opción "— Sin término —") llega como null desde el <select>.
    public Guid? TerminoPagoId { get; set; }

    // El importe viaja como texto y se interpreta con EntradaDecimal (ver ese tipo): el binder numérico
    // depende de la cultura del servidor y leería "1500,50" como 150050.
    public string? LimiteCreditoTexto { get; set; } = EntradaDecimal.Formatear(0m);

    public BloqueoSocioNegocio Bloqueado { get; set; } = BloqueoSocioNegocio.Ninguno;

    // Clasificación contable (Task 5.3). "" llega como null; un <select> deshabilitado (API de grupos caída) no se envía y
    // también llega como null: en la modificación la API lo trata como "conservar".
    public Guid? GrupoNegocioId { get; set; }
    public Guid? GrupoIvaNegocioId { get; set; }
    public Guid? GrupoClienteContableId { get; set; }

    /// <summary>Importe ya interpretado; 0 si el texto no es válido (la validación lo señala antes).</summary>
    public decimal LimiteCredito => EntradaDecimal.TryParse(LimiteCreditoTexto, out var valor, out _) ? valor : 0m;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!EntradaDecimal.TryParse(LimiteCreditoTexto, out _, out var error))
        {
            yield return new ValidationResult(error, [nameof(LimiteCreditoTexto)]);
        }
    }
}
