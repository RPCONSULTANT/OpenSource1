using OpenSource1.Core.Abstractions;
using OpenSource1.Core.Common;

namespace OpenSource1.Core.ValueObjects;

public sealed class DireccionCliente : ValueObject
{
    public DireccionCliente(string linea1, string? linea2)
    {
        if (string.IsNullOrWhiteSpace(linea1))
        {
            throw new ErroresDeDominioException(new Error(
                "direccion.linea1_requerida", "La línea 1 de la dirección es obligatoria.", nameof(linea1)));
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
