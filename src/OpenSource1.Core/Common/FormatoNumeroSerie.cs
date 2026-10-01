using System.Globalization;

namespace OpenSource1.Core.Common;

/// <summary>Número de serie descompuesto: prefijo (texto antes de los dígitos finales), valor numérico y ancho de esos dígitos.</summary>
public readonly record struct NumeroSerie(string Prefijo, long Valor, int Ancho)
{
    public string Texto => FormatoNumeroSerie.Formatear(Prefijo, Valor, Ancho);

    /// <summary>Mismo prefijo (ordinal, sensible a mayúsculas) y mismo ancho numérico.</summary>
    public bool MismoFormato(NumeroSerie otro) => string.Equals(Prefijo, otro.Prefijo, StringComparison.Ordinal) && Ancho == otro.Ancho;
}

/// <summary>
/// Formato de los números de serie con prefijo (spec no-series): <c>prefijo + valor.PadLeft(ancho, '0')</c>. Las series solo
/// dígitos tienen prefijo vacío y se comportan igual que antes. Máximo 20 caracteres (todas las columnas de número son varchar(20))
/// y 18 dígitos (cabe en <see cref="long"/>).
/// </summary>
public static class FormatoNumeroSerie
{
    public const int LongitudMaxima = 20;
    public const int DigitosMaximos = 18;

    public static bool TryParse(string? texto, out NumeroSerie numero)
    {
        numero = default;
        if (string.IsNullOrEmpty(texto) || texto.Length > LongitudMaxima || !string.Equals(texto.Trim(), texto, StringComparison.Ordinal))
        {
            return false;
        }

        var inicio = texto.Length;
        while (inicio > 0 && char.IsAsciiDigit(texto[inicio - 1]))
        {
            inicio--;
        }

        var ancho = texto.Length - inicio;
        if (ancho is 0 or > DigitosMaximos || !PrefijoValido(texto.AsSpan(0, inicio)))
        {
            return false;
        }

        numero = new NumeroSerie(texto[..inicio], long.Parse(texto[inicio..], NumberStyles.None, CultureInfo.InvariantCulture), ancho);
        return true;
    }

    /// <summary>El prefijo no admite caracteres de control (saltos de línea, tabuladores, NUL) ni separadores de línea/párrafo Unicode.</summary>
    private static bool PrefijoValido(ReadOnlySpan<char> prefijo)
    {
        foreach (var c in prefijo)
        {
            if (char.IsControl(c) || c is '\u2028' or '\u2029')
            {
                return false;
            }
        }

        return true;
    }

    public static string Formatear(string prefijo, long valor, int ancho) =>
        prefijo + valor.ToString(CultureInfo.InvariantCulture).PadLeft(ancho, '0');
}
