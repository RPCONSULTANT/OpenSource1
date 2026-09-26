using System.Text.RegularExpressions;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.GruposContables;

/// <summary>
/// Validación de <c>Codigo</c>/<c>Descripcion</c> común a los seis grupos contables (los cinco simples y el de cliente, que
/// pasa su propio prefijo de código de error). Solo reglas sin base de datos: la unicidad del código la garantiza el índice
/// único parcial de cada tabla (23505 se traduce a 409 en el manejador global).
/// </summary>
public static partial class GrupoContableValidator
{
    public const string PrefijoGrupoContable = "grupo_contable";

    public static List<Error> Validar(string? codigo, string? descripcion, string prefijo = PrefijoGrupoContable)
    {
        var errores = new List<Error>();

        if (string.IsNullOrWhiteSpace(codigo))
        {
            errores.Add(new Error($"{prefijo}.codigo_requerido", "El código es obligatorio.", "Codigo"));
        }
        else if (!CodigoValido().IsMatch(NormalizarCodigo(codigo)))
        {
            errores.Add(new Error(
                $"{prefijo}.codigo_invalido",
                "El código debe tener entre 1 y 20 caracteres, usando solo letras, números, guion y guion bajo.",
                "Codigo"));
        }

        if (string.IsNullOrWhiteSpace(descripcion))
        {
            errores.Add(new Error($"{prefijo}.descripcion_requerida", "La descripción es obligatoria.", "Descripcion"));
        }
        else if (descripcion.Trim().Length > 100)
        {
            errores.Add(new Error($"{prefijo}.descripcion_invalida", "La descripción no puede superar los 100 caracteres.", "Descripcion"));
        }

        return errores;
    }

    /// <summary>El código se normaliza a mayúsculas, igual que Almacen/UnidadMedida.</summary>
    public static string NormalizarCodigo(string codigo) => codigo.Trim().ToUpperInvariant();

    [GeneratedRegex("^[A-Z0-9_-]{1,20}$")]
    private static partial Regex CodigoValido();
}
