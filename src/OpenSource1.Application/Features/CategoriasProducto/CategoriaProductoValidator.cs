using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.CategoriasProducto;

/// <summary>
/// Validación compartida entre Create/Update: CategoriaProducto es una entidad plana sin value
/// objects, así que las reglas viven aquí (mismo patrón que <c>UnidadMedidaValidator</c>).
/// Los máximos (30/100) son los de las columnas de texto libre que tuvo <c>Producto</c> (código/nombre de categoría).
/// </summary>
internal static class CategoriaProductoValidator
{
    public const int CodigoMaximo = 30;
    public const int NombreMaximo = 100;

    public static IReadOnlyList<Error> Validar(string codigo, string nombre)
    {
        var errores = new List<Error>();

        if (string.IsNullOrWhiteSpace(codigo))
        {
            errores.Add(new Error("categoria_producto.codigo_requerido", "El código es obligatorio.", "Codigo"));
        }
        else if (codigo.Trim().Length > CodigoMaximo)
        {
            errores.Add(new Error(
                "categoria_producto.codigo_invalido", $"El código no puede superar los {CodigoMaximo} caracteres.", "Codigo"));
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            errores.Add(new Error("categoria_producto.nombre_requerido", "El nombre es obligatorio.", "Nombre"));
        }
        else if (nombre.Trim().Length > NombreMaximo)
        {
            errores.Add(new Error(
                "categoria_producto.nombre_invalido", $"El nombre no puede superar los {NombreMaximo} caracteres.", "Nombre"));
        }

        return errores;
    }

    /// <summary>El código de categoría se normaliza a mayúsculas, igual que hacía el value object legado.</summary>
    public static string NormalizarCodigo(string codigo) => codigo.Trim().ToUpperInvariant();
}
