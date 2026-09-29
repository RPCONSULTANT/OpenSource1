using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.UnidadesMedida;

/// <summary>
/// Validación compartida entre Create/Update: UnidadMedida es una entidad plana sin value
/// objects, así que las reglas viven aquí (mismo patrón que <c>TerminoPagoValidator</c>).
/// </summary>
internal static class UnidadMedidaValidator
{
    public const int DecimalesMaximos = 6;

    public static IReadOnlyList<Error> Validar(string codigo, string nombre, short decimales)
    {
        var errores = new List<Error>();

        if (string.IsNullOrWhiteSpace(codigo))
        {
            errores.Add(new Error("unidad_medida.codigo_requerido", "El código es obligatorio.", "Codigo"));
        }
        else if (codigo.Trim().Length > 10)
        {
            errores.Add(new Error("unidad_medida.codigo_invalido", "El código no puede superar los 10 caracteres.", "Codigo"));
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            errores.Add(new Error("unidad_medida.nombre_requerido", "El nombre es obligatorio.", "Nombre"));
        }
        else if (nombre.Trim().Length > 50)
        {
            errores.Add(new Error("unidad_medida.nombre_invalido", "El nombre no puede superar los 50 caracteres.", "Nombre"));
        }

        if (decimales < 0 || decimales > DecimalesMaximos)
        {
            errores.Add(new Error(
                "unidad_medida.decimales_invalido", $"Los decimales deben estar entre 0 y {DecimalesMaximos}.", "Decimales"));
        }

        return errores;
    }

    /// <summary>El código de unidad se normaliza a mayúsculas, igual que hacía el catálogo estático.</summary>
    public static string NormalizarCodigo(string codigo) => codigo.Trim().ToUpperInvariant();
}
