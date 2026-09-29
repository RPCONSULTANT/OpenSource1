using System.ComponentModel.DataAnnotations;
using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Components;

public sealed class ProductoEditorForm : IValidatableObject
{
    [Required(ErrorMessage = "El código es obligatorio.")]
    [MaxLength(50, ErrorMessage = "Máximo 50 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(200, ErrorMessage = "Máximo 200 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    // Los importes viajan como texto y se interpretan con EntradaDecimal (ver ese tipo): el binder numérico depende de la cultura del
    // servidor y leería "1500,50" como 150050.
    public string? PrecioVentaTexto { get; set; } = EntradaDecimal.Formatear(0m);

    // Stock (Task 3.6): eliminado del alta/edición. La existencia se deriva del libro de inventario y se registra
    // con diarios de inventario (Fase 4), no desde la ficha del producto.

    // "" (opción "— Por defecto —" del alta) llega como null desde el <select>: la API usa la categoría GENERAL / la unidad UND.
    public Guid? CategoriaId { get; set; }

    public Guid? UnidadMedidaBaseId { get; set; }

    public MetodoCosteo MetodoCosteo { get; set; } = MetodoCosteo.Promedio;

    public string? CostoEstandarTexto { get; set; } = EntradaDecimal.Formatear(0m);

    public BloqueoProducto Bloqueado { get; set; } = BloqueoProducto.Ninguno;

    public string? ImagePath { get; set; }

    // Clasificación contable (Task 5.3). "" (opción vacía: "— Por defecto —" en el alta = semilla, "— Sin asignar —" en la edición) llega como null; un <select> deshabilitado (API de grupos
    // caída) no se envía y también llega como null: en la modificación la API lo trata como "conservar".
    public Guid? GrupoProductoId { get; set; }
    public Guid? GrupoIvaProductoId { get; set; }
    public Guid? GrupoInventarioId { get; set; }

    /// <summary>Importe ya interpretado; 0 si el texto no es válido (la validación lo señala antes).</summary>
    public decimal PrecioVenta => EntradaDecimal.TryParse(PrecioVentaTexto, out var valor, out _, EtiquetaPrecioVenta) ? valor : 0m;

    public decimal CostoEstandar => EntradaDecimal.TryParse(CostoEstandarTexto, out var valor, out _, EtiquetaCostoEstandar) ? valor : 0m;

    private const string EtiquetaPrecioVenta = "El precio de venta";
    private const string EtiquetaCostoEstandar = "El costo estándar";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var error in ValidarImporte(PrecioVentaTexto, EtiquetaPrecioVenta, nameof(PrecioVentaTexto)))
        {
            yield return error;
        }

        foreach (var error in ValidarImporte(CostoEstandarTexto, EtiquetaCostoEstandar, nameof(CostoEstandarTexto)))
        {
            yield return error;
        }
    }

    private static IEnumerable<ValidationResult> ValidarImporte(string? texto, string etiqueta, string campo)
    {
        if (!EntradaDecimal.TryParse(texto, out var valor, out var error, etiqueta))
        {
            yield return new ValidationResult(error, [campo]);
        }
        else if (valor < 0)
        {
            yield return new ValidationResult($"{etiqueta} no puede ser negativo.", [campo]);
        }
        else if (decimal.Round(valor, 4) != valor)
        {
            yield return new ValidationResult($"{etiqueta} admite como máximo 4 decimales.", [campo]);
        }
    }
}
