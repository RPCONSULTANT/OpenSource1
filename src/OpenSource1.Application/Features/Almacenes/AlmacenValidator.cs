using System.Text.RegularExpressions;
using OpenSource1.Core.Common;
using OpenSource1.Core.ValueObjects;

namespace OpenSource1.Application.Features.Almacenes;

/// <summary>
/// Validación compartida entre Create/Update: Almacen es una entidad plana sin value objects,
/// así que las reglas viven aquí (mismo patrón que <c>UnidadMedidaValidator</c>). Solo valida lo
/// que no requiere base de datos; la unicidad de <c>Codigo</c> y de "predeterminado" las garantiza
/// el índice único parcial de Postgres (23505 se traduce a 409 en el handler global).
/// </summary>
internal static partial class AlmacenValidator
{
    public static IReadOnlyList<Error> Validar(
        string codigo, string nombre, string? direccionLinea1, string? direccionLinea2, string? ciudad, string? paisCodigo)
    {
        var errores = new List<Error>();

        if (string.IsNullOrWhiteSpace(codigo))
        {
            errores.Add(new Error("almacen.codigo_requerido", "El código es obligatorio.", "Codigo"));
        }
        else if (!CodigoValido().IsMatch(NormalizarCodigo(codigo)))
        {
            errores.Add(new Error(
                "almacen.codigo_invalido",
                "El código debe tener entre 1 y 10 caracteres, usando solo letras, números, guion y guion bajo.",
                "Codigo"));
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            errores.Add(new Error("almacen.nombre_requerido", "El nombre es obligatorio.", "Nombre"));
        }
        else if (nombre.Trim().Length > 100)
        {
            errores.Add(new Error("almacen.nombre_invalido", "El nombre no puede superar los 100 caracteres.", "Nombre"));
        }

        if (Longitud(direccionLinea1) > 300)
        {
            errores.Add(new Error(
                "almacen.direccion_invalida", "La línea 1 de la dirección no puede superar los 300 caracteres.", "DireccionLinea1"));
        }

        if (Longitud(direccionLinea2) > 300)
        {
            errores.Add(new Error(
                "almacen.direccion_invalida", "La línea 2 de la dirección no puede superar los 300 caracteres.", "DireccionLinea2"));
        }

        if (Longitud(ciudad) > 100)
        {
            errores.Add(new Error("almacen.ciudad_invalida", "La ciudad no puede superar los 100 caracteres.", "Ciudad"));
        }

        if (!string.IsNullOrWhiteSpace(paisCodigo) && !Pais.EsCodigoValido(paisCodigo))
        {
            errores.Add(new Error("pais.codigo_invalido", "El código de país no es válido.", "PaisCodigo"));
        }

        return errores;
    }

    /// <summary>El código de almacén se normaliza a mayúsculas, igual que UnidadMedida.</summary>
    public static string NormalizarCodigo(string codigo) => codigo.Trim().ToUpperInvariant();

    private static int Longitud(string? valor) => valor?.Trim().Length ?? 0;

    [GeneratedRegex("^[A-Z0-9_-]{1,10}$")]
    private static partial Regex CodigoValido();
}
