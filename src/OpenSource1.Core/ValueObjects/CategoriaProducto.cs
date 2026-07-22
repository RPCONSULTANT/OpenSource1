using OpenSource1.Core.Abstractions;

namespace OpenSource1.Core.ValueObjects;

public sealed class CategoriaProducto : ValueObject
{
    public CategoriaProducto(string codigo, string nombre)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

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
