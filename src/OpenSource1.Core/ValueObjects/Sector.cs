using OpenSource1.Core.Abstractions;
using OpenSource1.Core.Common;

namespace OpenSource1.Core.ValueObjects;

public sealed class Sector : ValueObject
{
    /// <param name="nombre">Nombre del sector.</param>
    /// <param name="nombreCampo">
    /// Nombre del campo tal como lo conoce el llamante (p. ej. <c>nameof(request.Sector)</c>).
    /// Si no se indica se usa <c>nameof(nombre)</c> como respaldo.
    /// </param>
    public Sector(string nombre, string? nombreCampo = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ErroresDeDominioException(new Error(
                "sector.nombre_requerido", "El nombre del sector es obligatorio.", nombreCampo ?? nameof(nombre)));
        }

        Nombre = nombre.Trim();
    }

    public string Nombre { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Nombre;
    }
}
