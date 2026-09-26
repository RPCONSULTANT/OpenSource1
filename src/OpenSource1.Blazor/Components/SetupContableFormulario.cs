using System.ComponentModel.DataAnnotations;
using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Components;

/// <summary>
/// Formulario de alta/modificación de una fila de setup contable (página <c>/setups-contables</c>, Task 5.4). Es UNO para los
/// tres tipos: los ejes son genéricos (<see cref="SecundarioId"/> = grupo de negocio / de IVA de negocio / almacén, vacío =
/// comodín; <see cref="PrincipalId"/> = grupo de producto / de IVA de producto / de inventario) y las cuentas van en ranuras
/// (<see cref="Cuenta1Id"/>..<see cref="Cuenta4Id"/>) cuyo significado da el tipo (ver <c>SetupsContables.razor</c>). El
/// porcentaje viaja como texto y se interpreta con <see cref="EntradaDecimal"/>.
/// </summary>
public sealed class SetupContableForm : IValidatableObject
{
    public const string EtiquetaPorcentaje = "El porcentaje de IVA";

    public Guid Id { get; set; }

    public long Xmin { get; set; }

    // "" (opción "— Cualquiera —") llega como null desde el <select>: comodín.
    public Guid? SecundarioId { get; set; }

    [Required(ErrorMessage = "Seleccione el grupo del eje principal.")]
    public Guid? PrincipalId { get; set; }

    public Guid? Cuenta1Id { get; set; }

    public Guid? Cuenta2Id { get; set; }

    public Guid? Cuenta3Id { get; set; }

    public Guid? Cuenta4Id { get; set; }

    // Solo setup de IVA.
    public string? PorcentajeIvaTexto { get; set; } = FormatearPorcentaje(0m);

    [MaxLength(20, ErrorMessage = "El identificador de IVA no puede superar los 20 caracteres.")]
    public string? IdentificadorIva { get; set; }

    // int y no el enum: el binder de formularios de Blazor SSR no se usa con enums en este proyecto (lección de la 5.2).
    public int TipoCalculoIva { get; set; } = (int)Core.Enums.TipoCalculoIva.Normal;

    public decimal PorcentajeIva => EntradaDecimal.TryParse(PorcentajeIvaTexto, out var valor, out _, EtiquetaPorcentaje) ? valor : 0m;

    public Guid? Cuenta(int ranura) => ranura switch
    {
        1 => Cuenta1Id,
        2 => Cuenta2Id,
        3 => Cuenta3Id,
        4 => Cuenta4Id,
        _ => throw new ArgumentOutOfRangeException(nameof(ranura))
    };

    /// <summary>Formato de edición del porcentaje: punto decimal, 2 a 5 decimales.</summary>
    public static string FormatearPorcentaje(decimal valor) => valor.ToString("0.00###", System.Globalization.CultureInfo.InvariantCulture);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!EntradaDecimal.TryParse(PorcentajeIvaTexto, out _, out var error, EtiquetaPorcentaje))
        {
            yield return new ValidationResult(error, [nameof(PorcentajeIvaTexto)]);
        }
    }
}
