using System.Text.RegularExpressions;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.CuentasContables;

/// <summary>
/// Validación compartida entre Create/Update. CuentaContable es una entidad plana sin value objects,
/// mismo patrón que <c>AlmacenValidator</c>/<c>UnidadMedidaValidator</c>. La unicidad de
/// <c>Numero</c> la garantiza el índice único parcial de Postgres (23505 se traduce a 409 en el
/// manejador global).
/// </summary>
internal static partial class CuentaContableValidator
{
    public static IReadOnlyList<Error> Validar(
        string numero, string nombre, TipoCuentaContable tipoCuenta, TipoResultadoCuenta tipoResultado, int sangria)
    {
        var errores = new List<Error>();

        if (string.IsNullOrWhiteSpace(numero))
        {
            errores.Add(new Error("cuenta_contable.numero_requerido", "El número es obligatorio.", "Numero"));
        }
        else if (!NumeroValido().IsMatch(NormalizarNumero(numero)))
        {
            errores.Add(new Error(
                "cuenta_contable.numero_invalido",
                "El número debe tener entre 1 y 20 caracteres, usando solo dígitos, punto y guion.",
                "Numero"));
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            errores.Add(new Error("cuenta_contable.nombre_requerido", "El nombre es obligatorio.", "Nombre"));
        }
        else if (nombre.Trim().Length > 100)
        {
            errores.Add(new Error("cuenta_contable.nombre_invalido", "El nombre no puede superar los 100 caracteres.", "Nombre"));
        }

        if (!Enum.IsDefined(tipoCuenta))
        {
            errores.Add(new Error("cuenta_contable.tipo_cuenta_invalido", "El tipo de cuenta no es válido.", "TipoCuenta"));
        }

        if (!Enum.IsDefined(tipoResultado))
        {
            errores.Add(new Error("cuenta_contable.tipo_resultado_invalido", "El tipo de resultado no es válido.", "TipoResultado"));
        }

        if (sangria is < 0 or > 10)
        {
            errores.Add(new Error("cuenta_contable.sangria_invalida", "La sangría debe estar entre 0 y 10.", "Sangria"));
        }

        return errores;
    }

    public static string NormalizarNumero(string numero) => numero.Trim();

    [GeneratedRegex("^[0-9.-]{1,20}$")]
    private static partial Regex NumeroValido();
}
