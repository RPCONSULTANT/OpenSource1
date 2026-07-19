using OpenSource1.Core.Abstractions;

namespace OpenSource1.Core.ValueObjects;

public sealed class DireccionCliente : ValueObject
{
    public DireccionCliente(string linea1, string? linea2)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(linea1);

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
