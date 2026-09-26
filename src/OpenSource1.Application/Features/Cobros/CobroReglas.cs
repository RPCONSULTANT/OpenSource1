using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Cobros;

/// <summary>Reglas de entrada comunes al pago y a la aplicación (Task 6.5).</summary>
internal static class CobroReglas
{
    /// <summary>Cota de <c>numeric(18,4)</c>: 14 dígitos enteros.</summary>
    private const decimal ImporteMaximo = 99_999_999_999_999.99m;

    /// <summary>&gt; 0, 2 decimales como máximo (importe de un documento legal) y dentro de la cota de la columna.</summary>
    public static bool ImporteValido(decimal importe) =>
        importe > 0m && importe <= ImporteMaximo && decimal.Round(importe, 2) == importe;

    public static Error ImporteInvalido(string que) => new(
        "cobro.importe_invalido", $"{que} debe ser mayor que cero, menor que 1e14 y tener como máximo 2 decimales.", "Importe");
}
