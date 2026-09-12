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
    /// <remarks>
    /// Esta sobrecarga de un parámetro existe para que EF Core pueda enlazar el
    /// constructor del <c>ComplexProperty</c> (que solo mapea <c>Nombre</c>). No usar un
    /// valor por defecto en <paramref name="nombreCampo"/> de la sobrecarga completa evita
    /// ambigüedad de resolución de sobrecarga por número de argumentos.
    /// </remarks>
    public Sector(string nombre)
        : this(nombre, null)
    {
    }

    public Sector(string nombre, string? nombreCampo)
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
