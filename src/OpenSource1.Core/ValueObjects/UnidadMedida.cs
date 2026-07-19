using OpenSource1.Core.Abstractions;

namespace OpenSource1.Core.ValueObjects;

public sealed class UnidadMedida : ValueObject
{
    private static readonly IReadOnlyDictionary<string, string> Nombres = new Dictionary<string, string>
    {
        ["UND"] = "Unidad",
        ["KG"] = "Kilogramo",
        ["GR"] = "Gramo",
        ["LT"] = "Litro",
        ["ML"] = "Mililitro",
        ["CJA"] = "Caja",
        ["DOC"] = "Docena",
        ["PAQ"] = "Paquete",
        ["MT"] = "Metro",
        ["LB"] = "Libra",
    };

    public static IReadOnlyList<(string Codigo, string Nombre)> Catalogo { get; } =
        [.. Nombres.Select(kv => (kv.Key, kv.Value))];

    private UnidadMedida(string codigo, string nombre)
    {
        Codigo = codigo;
        Nombre = nombre;
    }

    public string Codigo { get; }
    public string Nombre { get; }

    public static bool EsCodigoValido(string? codigo) =>
        !string.IsNullOrWhiteSpace(codigo) && Nombres.ContainsKey(codigo.Trim().ToUpperInvariant());

    public static UnidadMedida Of(string codigo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        var normalizado = codigo.Trim().ToUpperInvariant();

        if (!Nombres.TryGetValue(normalizado, out var nombre))
        {
            throw new ArgumentException($"Código de unidad de medida no reconocido: '{codigo}'.", nameof(codigo));
        }

        return new UnidadMedida(normalizado, nombre);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Codigo;
    }
}
