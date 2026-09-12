using OpenSource1.Core.Abstractions;
using OpenSource1.Core.Common;

namespace OpenSource1.Core.ValueObjects;

public sealed class CategoriaProducto : ValueObject
{
    /// <param name="codigo">Código de la categoría.</param>
    /// <param name="nombre">Nombre de la categoría.</param>
    /// <param name="nombreCampoCodigo">
    /// Nombre del campo de <paramref name="codigo"/> tal como lo conoce el llamante (p. ej.
    /// <c>nameof(request.CategoriaCodigo)</c>). Si no se indica se usa <c>nameof(codigo)</c>.
    /// </param>
    /// <param name="nombreCampoNombre">
    /// Igual que <paramref name="nombreCampoCodigo"/> pero para <paramref name="nombre"/>.
    /// </param>
    /// <remarks>
    /// Esta sobrecarga de dos parámetros existe para que EF Core pueda enlazar el
    /// constructor del <c>ComplexProperty</c> (que solo mapea <c>Codigo</c>/<c>Nombre</c>).
    /// No usar valores por defecto en los <c>nombreCampo*</c> de la sobrecarga completa
    /// evita ambigüedad de resolución de sobrecarga por número de argumentos.
    /// </remarks>
    public CategoriaProducto(string codigo, string nombre)
        : this(codigo, nombre, null, null)
    {
    }

    public CategoriaProducto(string codigo, string nombre, string? nombreCampoCodigo, string? nombreCampoNombre)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            throw new ErroresDeDominioException(new Error(
                "categoria_producto.codigo_requerido", "El código de categoría es obligatorio.", nombreCampoCodigo ?? nameof(codigo)));
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ErroresDeDominioException(new Error(
                "categoria_producto.nombre_requerido", "El nombre de categoría es obligatorio.", nombreCampoNombre ?? nameof(nombre)));
        }

        Codigo = codigo.Trim().ToUpperInvariant();
        Nombre = nombre.Trim();
    }

    public string Codigo { get; }
    public string Nombre { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Codigo;
        yield return Nombre;
    }
}
