using System.ComponentModel.DataAnnotations;

namespace OpenSource1.Blazor.Components;

/// <summary>
/// Formulario de una línea de borrador de nota de crédito (alta y modificación, Task 8.7). Solo la cantidad y la devolución de
/// inventario son editables: precio, descuento e IVA se copian de la línea de la factura en la API. La cantidad viaja como texto y
/// se lee con <see cref="EntradaDecimal"/> (independiente de la cultura); el resto de reglas (≤ pendiente, exactitud de la
/// devolución, importe 0) las valida la API y la página muestra su mensaje.
/// </summary>
public sealed class LineaNotaCreditoForm : IValidatableObject
{
    /// <summary>Línea de la factura que se acredita (solo en el alta; campo oculto).</summary>
    public long LineaFacturaVentaId { get; set; }

    public string? CantidadTexto { get; set; }

    /// <summary>Casilla: sin marcar no viaja en el POST y queda en <see langword="false"/>.</summary>
    public bool DevolverInventario { get; set; }

    public long Xmin { get; set; }

    public decimal? Cantidad =>
        !string.IsNullOrWhiteSpace(CantidadTexto) && EntradaDecimal.TryParse(CantidadTexto, out var valor, out _) ? valor : null;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(CantidadTexto))
        {
            yield return new ValidationResult("La cantidad es obligatoria.", [nameof(CantidadTexto)]);
        }
        else if (!EntradaDecimal.TryParse(CantidadTexto, out _, out var error, "La cantidad"))
        {
            yield return new ValidationResult(error ?? "La cantidad no es válida.", [nameof(CantidadTexto)]);
        }
    }
}
