using System.Text.RegularExpressions;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.Series;

public static partial class SerieReglas
{
    public const int LongitudDescripcion = 200;

    public static string NormalizarCodigo(string? codigo) => (codigo ?? string.Empty).Trim().ToUpperInvariant();

    public static Error NoEncontrada() => new("serie.no_encontrado", "No se encontró la serie de numeración solicitada.", "Id");

    public static Error LineaNoEncontrada() => new("linea_serie.no_encontrado", "No se encontró la línea de serie solicitada.", "Id");

    public static List<Error> ValidarCabecera(string codigo, string? descripcion, TipoDocumentoSerie tipo)
    {
        var errores = new List<Error>();
        if (!CodigoValido().IsMatch(codigo))
        {
            errores.Add(new Error("serie.codigo_invalido", "El código admite de 1 a 20 letras, dígitos, guiones o guiones bajos.", "Codigo"));
        }

        if (string.IsNullOrWhiteSpace(descripcion) || descripcion.Trim().Length > LongitudDescripcion)
        {
            errores.Add(new Error("serie.descripcion_invalida", "La descripción es obligatoria (máximo 200 caracteres).", "Descripcion"));
        }

        if (!TipoDocumentoSerieNombres.EsValido(tipo))
        {
            errores.Add(new Error("serie.tipo_invalido", "Indique el tipo de documento que numera la serie.", "TipoDocumento"));
        }

        return errores;
    }

    /// <summary>Formato y rango de una línea (sin mirar la base de datos). <paramref name="ultimoNumeroUsado"/> vacío = sin usar.</summary>
    public static List<Error> ValidarLinea(
        string? numeroInicial, string? numeroFinal, string? numeroAviso, string? ultimoNumeroUsado, int incremento, DateOnly fechaInicial)
    {
        var errores = new List<Error>();
        var inicialOk = FormatoNumeroSerie.TryParse(numeroInicial, out var inicial);
        var finalOk = FormatoNumeroSerie.TryParse(numeroFinal, out var final);
        if (!inicialOk)
        {
            errores.Add(NumeroInvalido("NumeroInicial", "inicial"));
        }

        if (!finalOk)
        {
            errores.Add(NumeroInvalido("NumeroFinal", "final"));
        }

        if (inicialOk && inicial.Valor < 1)
        {
            // NS-S1: el primer número emitible es el inicial; 0 confundiría la línea nueva con la semilla "0…0" (= sin usar).
            errores.Add(new Error("linea_serie.rango_invalido", "El número inicial debe ser 1 o mayor.", "NumeroInicial"));
        }

        if (inicialOk && finalOk)
        {
            if (!inicial.MismoFormato(final))
            {
                errores.Add(new Error("linea_serie.formato_distinto", "El número final debe tener el mismo prefijo y el mismo ancho que el inicial.", "NumeroFinal"));
            }
            else if (final.Valor <= inicial.Valor)
            {
                errores.Add(new Error("linea_serie.rango_invalido", "El número final debe ser mayor que el inicial.", "NumeroFinal"));
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(numeroAviso)
                    && !(FormatoNumeroSerie.TryParse(numeroAviso, out var aviso) && aviso.MismoFormato(inicial)
                         && aviso.Valor >= inicial.Valor && aviso.Valor <= final.Valor))
                {
                    errores.Add(new Error("linea_serie.aviso_invalido", "El número de aviso debe tener el formato del rango y estar dentro de él.", "NumeroAviso"));
                }

                if (!string.IsNullOrWhiteSpace(ultimoNumeroUsado)
                    && !(FormatoNumeroSerie.TryParse(ultimoNumeroUsado, out var ultimo) && ultimo.MismoFormato(inicial)
                         && ultimo.Valor >= inicial.Valor && ultimo.Valor <= final.Valor))
                {
                    errores.Add(new Error(
                        "linea_serie.ultimo_invalido",
                        "El último número usado debe tener el formato del rango y estar dentro de él (déjelo vacío si la línea no se ha usado).",
                        "UltimoNumeroUsado"));
                }
            }
        }

        if (incremento < 1)
        {
            errores.Add(new Error("linea_serie.incremento_invalido", "El incremento debe ser 1 o mayor.", "Incremento"));
        }

        if (fechaInicial == default)
        {
            errores.Add(new Error("linea_serie.fecha_invalida", "La fecha inicial es obligatoria.", "FechaInicial"));
        }

        return errores;
    }

    /// <summary>
    /// Dos líneas de series del MISMO tipo con el mismo prefijo y ancho y rangos que se cruzan emitirían el mismo número (PK de la
    /// factura, único del borrador…): se rechaza (Review Focus 1). <paramref name="excluirLineaId"/> = la línea que se modifica.
    /// </summary>
    public static Error? Solapada(string numeroInicial, string numeroFinal, IEnumerable<LineaDeTipo> lineas, Guid? excluirLineaId)
    {
        if (!FormatoNumeroSerie.TryParse(numeroInicial, out var inicial) || !FormatoNumeroSerie.TryParse(numeroFinal, out var final))
        {
            return null;
        }

        foreach (var l in lineas.Where(l => l.LineaId != excluirLineaId))
        {
            if (FormatoNumeroSerie.TryParse(l.NumeroInicial, out var li) && FormatoNumeroSerie.TryParse(l.NumeroFinal, out var lf)
                && inicial.MismoFormato(li) && inicial.Valor <= lf.Valor && li.Valor <= final.Valor)
            {
                return new Error(
                    "linea_serie.solapada",
                    $"El rango se solapa con la línea {l.NumeroInicial}–{l.NumeroFinal} de la serie {l.SerieCodigo} (mismo tipo de documento): " +
                    "los documentos repetirían número. Use otro prefijo u otro rango.",
                    "NumeroInicial");
            }
        }

        return null;
    }

    private static Error NumeroInvalido(string campo, string cual) => new(
        "linea_serie.numero_invalido",
        $"El número {cual} debe terminar en dígitos (máximo 20 caracteres y 18 dígitos), p. ej. FV-000001.",
        campo);

    [GeneratedRegex("^[A-Z0-9_-]{1,20}$")]
    private static partial Regex CodigoValido();
}
