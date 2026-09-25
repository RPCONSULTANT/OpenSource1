using System.Globalization;
using System.Text.RegularExpressions;

namespace OpenSource1.Blazor.Components;

/// <summary>
/// Lectura de importes escritos a mano en un formulario. El binder de Blazor SSR convierte los
/// decimales con la cultura del servidor (<c>1500,50</c> se leería como <c>150050</c> con la cultura
/// invariante, porque la coma es separador de miles): por eso el importe viaja como texto y se
/// interpreta aquí, sin depender de la cultura, aceptando punto o coma decimal.
/// </summary>
/// <remarks>
/// Reglas: los espacios solo valen como separador de miles entre grupos de 3 dígitos; cuando aparecen ambos separadores, el último es el decimal y el otro el de miles
/// (<c>1.500,50</c> / <c>1,500.50</c>); un único separador que se repite (<c>1,500,000</c>) es de
/// miles; un único separador con 1-2 o 4+ decimales es el decimal (<c>1500,50</c>, <c>1500.5</c>). Un
/// único separador seguido de exactamente 3 dígitos tras 1-3 dígitos sin ceros a la izquierda
/// (<c>1,500</c>, <c>1.500</c>) es AMBIGUO (¿mil quinientos o uno con cinco décimas?): se rechaza en
/// vez de adivinar, para no guardar en silencio un importe mil veces distinto del que el usuario quería.
/// </remarks>
public static partial class EntradaDecimal
{
    [GeneratedRegex(@"^-?\d{1,3}([ \u00A0\u2009\u202F]\d{3})+([.,]\d+)?$")]
    private static partial Regex EspaciosComoMiles();

    public const string MensajeInvalido =
        "El límite de crédito no es válido. Escríbalo con punto o coma decimal y sin separador de miles, por ejemplo 1500.50.";

    public const string MensajeAmbiguo =
        "El límite de crédito es ambiguo: con un solo separador y tres dígitos detrás no se sabe si es de miles o decimal. Escriba 1500.00 (o 1500 si no lleva decimales).";

    /// <summary>Intenta leer el importe. Vacío o solo espacios = 0. Devuelve el mensaje de error si falla.</summary>
    public static bool TryParse(string? texto, out decimal valor, out string? error)
    {
        valor = 0m;
        error = null;

        if (string.IsNullOrWhiteSpace(texto))
        {
            return true;
        }

        // Los espacios solo valen como separador de miles entre grupos de exactamente 3 dígitos ("1 500,5"); cualquier
        // otro espacio interno ("1 5 0 0", "1  500") se rechaza en vez de "pegar" los dígitos.
        var t = texto.Trim();
        if (t.Any(char.IsWhiteSpace))
        {
            if (!EspaciosComoMiles().IsMatch(t))
            {
                error = MensajeInvalido;
                return false;
            }

            t = new string(t.Where(c => !char.IsWhiteSpace(c)).ToArray());
        }

        var negativo = t.StartsWith('-');
        if (negativo)
        {
            t = t[1..];
        }

        if (t.Length == 0 || t.Any(c => !(char.IsAsciiDigit(c) || c == '.' || c == ',')) || !char.IsAsciiDigit(t[0]) || !char.IsAsciiDigit(t[^1]))
        {
            error = MensajeInvalido;
            return false;
        }

        var ultimo = t.LastIndexOfAny(['.', ',']);
        string enteros;
        string decimales = string.Empty;

        if (ultimo < 0)
        {
            enteros = t;
        }
        else
        {
            var sepUltimo = t[ultimo];
            var otro = sepUltimo == '.' ? ',' : '.';
            var antes = t[..ultimo];
            var despues = t[(ultimo + 1)..];

            if (antes.Contains(otro))
            {
                // Ambos separadores: el último es el decimal; el otro solo puede agrupar miles y no puede repetirse el decimal.
                if (antes.Contains(sepUltimo) || !AgrupacionDeMilesValida(antes, otro))
                {
                    error = MensajeInvalido;
                    return false;
                }

                enteros = antes.Replace(otro.ToString(), string.Empty);
                decimales = despues;
            }
            else if (antes.Contains(sepUltimo))
            {
                // Mismo separador repetido: solo puede ser agrupación de miles (1,500,000).
                if (!AgrupacionDeMilesValida(t, sepUltimo))
                {
                    error = MensajeInvalido;
                    return false;
                }

                enteros = t.Replace(sepUltimo.ToString(), string.Empty);
            }
            else
            {
                if (despues.Length == 3 && antes.Length is >= 1 and <= 3 && antes[0] != '0')
                {
                    error = MensajeAmbiguo;
                    return false;
                }

                enteros = antes;
                decimales = despues;
            }
        }

        var normalizado = decimales.Length == 0 ? enteros : $"{enteros}.{decimales}";
        if (!decimal.TryParse(normalizado, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out valor))
        {
            error = MensajeInvalido;
            return false;
        }

        if (negativo)
        {
            valor = -valor;
        }

        return true;
    }

    /// <summary>Formato de edición: siempre con punto decimal y al menos 2 decimales (hasta 4), sin separador de miles.</summary>
    public static string Formatear(decimal valor) => valor.ToString("0.00##", CultureInfo.InvariantCulture);

    // "1,500,000": un primer grupo de 1-3 dígitos y los demás de exactamente 3.
    private static bool AgrupacionDeMilesValida(string texto, char separador)
    {
        var grupos = texto.Split(separador);
        return grupos.Length >= 2
            && grupos[0].Length is >= 1 and <= 3
            && grupos[0].All(char.IsAsciiDigit)
            && grupos.Skip(1).All(g => g.Length == 3 && g.All(char.IsAsciiDigit));
    }
}
