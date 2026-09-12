using OpenSource1.Core.Abstractions;
using OpenSource1.Core.Common;

namespace OpenSource1.Core.ValueObjects;

public sealed class CategoriaProducto : ValueObject
{
    public CategoriaProducto(string codigo, string nombre)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            throw new ErroresDeDominioException(new Error(
                "categoria_producto.codigo_requerido", "El código de categoría es obligatorio.", nameof(codigo)));
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ErroresDeDominioException(new Error(
                "categoria_producto.nombre_requerido", "El nombre de categoría es obligatorio.", nameof(nombre)));
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
