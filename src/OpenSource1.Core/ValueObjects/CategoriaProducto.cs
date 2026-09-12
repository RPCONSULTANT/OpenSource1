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
    public CategoriaProducto(string codigo, string nombre, string? nombreCampoCodigo = null, string? nombreCampoNombre = null)
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
