using System.Text.RegularExpressions;

namespace OpenSource1.Core.Common;

/// <summary>
/// Cálculo puro del siguiente número de una línea de serie (lo usan el generador, la vista previa y el listado de series).
/// Una línea está USADA si su <c>UltimoNumeroUsado</c> no es menor que su <c>NumeroInicial</c> (la semilla "0…0" y el texto
/// vacío significan "sin usar"): sin usar, el siguiente es el número inicial; usada, el último más el incremento.
/// <see cref="Siguiente"/> rechaza (<c>numeracion.linea_invalida</c>) rangos invertidos, incrementos &lt; 1 y un último
/// usado no vacío que no se interprete o no comparta el formato de la línea.
/// </summary>
public static partial class CalculoNumeroSerie
{
    /// <summary>
    /// Falla cerrado: un último usado no vacío que no se interprete, con otro prefijo o más ancho que la línea cuenta como
    /// usado (la línea no se puede borrar ni cambiar como si estuviera libre). Solo null/"" o un valor menor que el inicial
    /// (semilla "0…0") significan "sin usar".
    /// </summary>
    public static bool EstaUsada(string numeroInicial, string? ultimoNumeroUsado)
    {
        if (string.IsNullOrEmpty(ultimoNumeroUsado))
        {
            return false;
        }

        if (!FormatoNumeroSerie.TryParse(numeroInicial, out var inicial)
            || !FormatoNumeroSerie.TryParse(ultimoNumeroUsado, out var ultimo)
            || !string.Equals(ultimo.Prefijo, inicial.Prefijo, StringComparison.Ordinal)
            || ultimo.Ancho > inicial.Ancho)
        {
            return true;
        }

        return ultimo.Valor >= inicial.Valor;
    }

    public static Result<NumeroGenerado> Siguiente(
        string codigoSerie, string numeroInicial, string numeroFinal, string? ultimoNumeroUsado, int incremento, string? numeroAviso)
    {
        if (!FormatoNumeroSerie.TryParse(numeroInicial, out var inicial)
            || !FormatoNumeroSerie.TryParse(numeroFinal, out var final)
            || !inicial.MismoFormato(final)
            || inicial.Valor > final.Valor
            || incremento < 1)
        {
            return LineaInvalida(codigoSerie);
        }

        // Solo null o "" significan "sin usar". Un último usado no vacío debe interpretarse, tener el mismo prefijo que la
        // línea y un ancho no mayor (se admite el contador heredado sin relleno, p. ej. "7" en una línea "00000001"):
        // cualquier otra cosa es un dato incoherente que podría reemitir o saltar números.
        var siguiente = inicial.Valor;
        if (!string.IsNullOrEmpty(ultimoNumeroUsado))
        {
            if (!FormatoNumeroSerie.TryParse(ultimoNumeroUsado, out var ultimo)
                || !string.Equals(ultimo.Prefijo, inicial.Prefijo, StringComparison.Ordinal)
                || ultimo.Ancho > inicial.Ancho)
            {
                return LineaInvalida(codigoSerie);
            }

            // Valores de hasta 18 dígitos + un int nunca desbordan long.
            if (ultimo.Valor >= inicial.Valor)
            {
                siguiente = ultimo.Valor + incremento;
            }
        }

        if (siguiente > final.Valor)
        {
            return Agotada(codigoSerie);
        }

        string? aviso = null;
        if (FormatoNumeroSerie.TryParse(numeroAviso, out var avisoNumero) && siguiente >= avisoNumero.Valor)
        {
            var restantes = (final.Valor - siguiente) / incremento;
            aviso = TextoAviso(codigoSerie, numeroAviso!, restantes);
        }

        return Result<NumeroGenerado>.Exito(new NumeroGenerado((inicial with { Valor = siguiente }).Texto, aviso));
    }

    /// <summary>Texto del aviso de numeración (número de aviso alcanzado); <see cref="EsTextoAviso"/> reconoce exactamente esta forma.</summary>
    public static string TextoAviso(string codigoSerie, string numeroAviso, long restantes) =>
        $"La serie {codigoSerie} alcanzó su número de aviso ({numeroAviso}): quedan {restantes} número(s) en la línea.";

    /// <summary>
    /// El posteo pasa el aviso a la página del documento por la query (<c>?aviso=</c>); esa página solo lo muestra si tiene la
    /// forma exacta de <see cref="TextoAviso"/>, para no reflejar texto libre de un enlace manipulado: código de serie sin espacios
    /// ni caracteres de control (admite los heredados, p. ej. con punto), número de aviso que <see cref="FormatoNumeroSerie.TryParse"/>
    /// interpreta (mismos caracteres de prefijo, incluido el espacio interior) y cantidad en dígitos ASCII, anclado con <c>\z</c>
    /// (sin salto de línea final).
    /// </summary>
    public static bool EsTextoAviso(string? texto)
    {
        if (string.IsNullOrEmpty(texto) || texto.Length > 200)
        {
            return false;
        }

        var coincidencia = AvisoRegex().Match(texto);
        return coincidencia.Success && FormatoNumeroSerie.TryParse(coincidencia.Groups["aviso"].Value, out _);
    }

    // Código: sin espacios ni controles (ni separadores de línea/párrafo). Aviso: los caracteres que admite FormatoNumeroSerie (sin
    // controles ni U+2028/U+2029) y luego TryParse decide. Cantidad: [0-9] (\d admitiría dígitos Unicode). \z: "$" admite "\n" final.
    [GeneratedRegex(@"\ALa serie [^\s\p{Cc}\u2028\u2029]{1,20} alcanzó su número de aviso \((?<aviso>[^\p{Cc}\u2028\u2029]{1,20})\): quedan [0-9]{1,19} número\(s\) en la línea\.\z", RegexOptions.CultureInvariant)]
    private static partial Regex AvisoRegex();

    private static Result<NumeroGenerado> LineaInvalida(string codigoSerie) => Result<NumeroGenerado>.Fallo(new Error(
        "numeracion.linea_invalida", $"La línea vigente de la serie '{codigoSerie}' no tiene un formato válido.", "SerieId"));

    private static Result<NumeroGenerado> Agotada(string codigoSerie) => Result<NumeroGenerado>.Fallo(new Error(
        "numeracion.serie_agotada", $"La serie '{codigoSerie}' agotó su rango numérico.", "SerieId"));
}
