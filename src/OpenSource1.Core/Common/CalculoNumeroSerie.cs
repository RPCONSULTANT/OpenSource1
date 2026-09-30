namespace OpenSource1.Core.Common;

/// <summary>
/// Cálculo puro del siguiente número de una línea de serie (lo usan el generador, la vista previa y el listado de series).
/// Una línea está USADA si su <c>UltimoNumeroUsado</c> no es menor que su <c>NumeroInicial</c> (la semilla "0…0" y el texto
/// vacío significan "sin usar"): sin usar, el siguiente es el número inicial; usada, el último más el incremento.
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
            || incremento < 1)
        {
            return Result<NumeroGenerado>.Fallo(new Error(
                "numeracion.linea_invalida", $"La línea vigente de la serie '{codigoSerie}' no tiene un formato válido.", "SerieId"));
        }

        long siguiente;
        if (EstaUsada(numeroInicial, ultimoNumeroUsado))
        {
            FormatoNumeroSerie.TryParse(ultimoNumeroUsado, out var ultimo);
            if (ultimo.Valor > long.MaxValue - incremento)
            {
                return Agotada(codigoSerie);
            }

            siguiente = ultimo.Valor + incremento;
        }
        else
        {
            siguiente = inicial.Valor;
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

    private static Result<NumeroGenerado> Agotada(string codigoSerie) => Result<NumeroGenerado>.Fallo(new Error(
        "numeracion.serie_agotada", $"La serie '{codigoSerie}' agotó su rango numérico.", "SerieId"));
}
