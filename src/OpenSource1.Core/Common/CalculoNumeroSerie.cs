namespace OpenSource1.Core.Common;

/// <summary>
/// Cálculo puro del siguiente número de una línea de serie (lo usan el generador, la vista previa y el listado de series).
/// Una línea está USADA si su <c>UltimoNumeroUsado</c> no es menor que su <c>NumeroInicial</c> (la semilla "0…0" y el texto
/// vacío significan "sin usar"): sin usar, el siguiente es el número inicial; usada, el último más el incremento.
/// <see cref="Siguiente"/> rechaza (<c>numeracion.linea_invalida</c>) rangos invertidos, incrementos &lt; 1 y un último
/// usado no vacío que no se interprete o no comparta el formato de la línea.
/// </summary>
public static class CalculoNumeroSerie
{
    public static bool EstaUsada(string numeroInicial, string? ultimoNumeroUsado) =>
        FormatoNumeroSerie.TryParse(numeroInicial, out var inicial)
        && FormatoNumeroSerie.TryParse(ultimoNumeroUsado, out var ultimo)
        && ultimo.Valor >= inicial.Valor;

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
            aviso = $"La serie {codigoSerie} alcanzó su número de aviso ({numeroAviso}): quedan {restantes} número(s) en la línea.";
        }

        return Result<NumeroGenerado>.Exito(new NumeroGenerado((inicial with { Valor = siguiente }).Texto, aviso));
    }

    private static Result<NumeroGenerado> LineaInvalida(string codigoSerie) => Result<NumeroGenerado>.Fallo(new Error(
        "numeracion.linea_invalida", $"La línea vigente de la serie '{codigoSerie}' no tiene un formato válido.", "SerieId"));

    private static Result<NumeroGenerado> Agotada(string codigoSerie) => Result<NumeroGenerado>.Fallo(new Error(
        "numeracion.serie_agotada", $"La serie '{codigoSerie}' agotó su rango numérico.", "SerieId"));
}
