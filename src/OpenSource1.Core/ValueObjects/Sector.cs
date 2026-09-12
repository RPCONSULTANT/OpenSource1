using OpenSource1.Core.Abstractions;
using OpenSource1.Core.Common;

namespace OpenSource1.Core.ValueObjects;

public sealed class Sector : ValueObject
{
    public Sector(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ErroresDeDominioException(new Error(
                "sector.nombre_requerido", "El nombre del sector es obligatorio.", nameof(nombre)));
        }

        Nombre = nombre.Trim();
    }

    public string Nombre { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Nombre;
    }
}
