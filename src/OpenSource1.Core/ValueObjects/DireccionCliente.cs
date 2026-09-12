using OpenSource1.Core.Abstractions;
using OpenSource1.Core.Common;

namespace OpenSource1.Core.ValueObjects;

public sealed class DireccionCliente : ValueObject
{
    /// <param name="linea1">Línea 1 de la dirección.</param>
    /// <param name="linea2">Línea 2 de la dirección (opcional, sin validación).</param>
    /// <param name="nombreCampoLinea1">
    /// Nombre del campo de <paramref name="linea1"/> tal como lo conoce el llamante (p. ej.
    /// <c>nameof(request.DireccionLinea1)</c>). Si no se indica se usa <c>nameof(linea1)</c>.
    /// </param>
    public DireccionCliente(string linea1, string? linea2, string? nombreCampoLinea1 = null)
    {
        if (string.IsNullOrWhiteSpace(linea1))
        {
            throw new ErroresDeDominioException(new Error(
                "direccion.linea1_requerida", "La línea 1 de la dirección es obligatoria.", nombreCampoLinea1 ?? nameof(linea1)));
        }

        Linea1 = linea1.Trim();
        Linea2 = string.IsNullOrWhiteSpace(linea2) ? null : linea2.Trim();
    }

    public string Linea1 { get; }
    public string? Linea2 { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Linea1;
        yield return Linea2;
    }
}
