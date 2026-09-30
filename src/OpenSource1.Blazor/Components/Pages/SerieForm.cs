using System.ComponentModel.DataAnnotations;
using OpenSource1.Blazor.Services;
using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Components.Pages;

/// <summary>Cabecera de una serie de numeración (alta en <c>/series/nueva</c> y modificación en la ficha).</summary>
public sealed class SerieForm
{
    [Required(ErrorMessage = "El código es obligatorio."), MaxLength(20, ErrorMessage = "Máximo 20 caracteres.")]
    public string? Codigo { get; set; }

    [Required(ErrorMessage = "La descripción es obligatoria."), MaxLength(200, ErrorMessage = "Máximo 200 caracteres.")]
    public string? Descripcion { get; set; }

    // [SupplyParameterFromForm] no enlaza enums de forma fiable desde <select>: el tipo viaja como entero.
    public short TipoDocumento { get; set; }

    public bool PermiteHuecos { get; set; }

    public bool Activa { get; set; }

    public long Xmin { get; set; }

    public SerieInput ToInput() => new(
        (Codigo ?? string.Empty).Trim(), (Descripcion ?? string.Empty).Trim(), (TipoDocumentoSerie)TipoDocumento, PermiteHuecos, Activa);
}

/// <summary>Línea (rango con prefijo) de una serie: fila de alta y fila de edición de la ficha.</summary>
public sealed class LineaSerieForm : IValidatableObject
{
    /// <summary>Línea que se modifica (oculto en la fila de edición; vacío en el alta y cuando no llegó el formulario).</summary>
    public Guid Id { get; set; }

    [Required(ErrorMessage = "El número inicial es obligatorio.")]
    public string? NumeroInicial { get; set; }

    [Required(ErrorMessage = "El número final es obligatorio.")]
    public string? NumeroFinal { get; set; }

    public string? NumeroAviso { get; set; }

    /// <summary>Solo en el alta (vacío = línea sin usar); en la modificación no viaja: el contador no se edita.</summary>
    public string? UltimoNumeroUsado { get; set; }

    [Required(ErrorMessage = "La fecha inicial es obligatoria.")]
    public string? FechaInicialTexto { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "El incremento debe ser 1 o mayor.")]
    public int Incremento { get; set; } = 1;

    public bool Bloqueada { get; set; }

    public long Xmin { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(FechaInicialTexto) && !EntradaFecha.TryParse(FechaInicialTexto, out _, out var error, "La fecha inicial"))
        {
            yield return new ValidationResult(error, [nameof(FechaInicialTexto)]);
        }
    }

    /// <summary>La fecha ya validada por <see cref="Validate"/>; en un POST sin validar (formulario vacío) viaja <c>default</c> y responde la API.</summary>
    public LineaSerieInput ToInput() => new(
        (NumeroInicial ?? string.Empty).Trim(), (NumeroFinal ?? string.Empty).Trim(),
        string.IsNullOrWhiteSpace(NumeroAviso) ? null : NumeroAviso.Trim(),
        string.IsNullOrWhiteSpace(UltimoNumeroUsado) ? null : UltimoNumeroUsado.Trim(),
        EntradaFecha.TryParse(FechaInicialTexto, out var fecha, out _) ? fecha : default, Incremento, Bloqueada);
}
