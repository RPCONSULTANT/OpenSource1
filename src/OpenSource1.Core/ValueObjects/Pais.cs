using OpenSource1.Core.Abstractions;
using OpenSource1.Core.Common;

namespace OpenSource1.Core.ValueObjects;

public sealed class Pais : ValueObject
{
    private static readonly IReadOnlyDictionary<string, string> Nombres = new Dictionary<string, string>
    {
        ["DO"] = "República Dominicana",
        ["US"] = "Estados Unidos",
        ["MX"] = "México",
        ["CO"] = "Colombia",
        ["PA"] = "Panamá",
        ["ES"] = "España",
        ["PR"] = "Puerto Rico",
        ["HT"] = "Haití",
        ["VE"] = "Venezuela",
        ["CN"] = "China",
    };

    public static IReadOnlyList<(string Codigo, string Nombre)> Catalogo { get; } =
        [.. Nombres.Select(kv => (kv.Key, kv.Value))];

    private Pais(string codigo, string nombre)
    {
        Codigo = codigo;
        Nombre = nombre;
    }

    public string Codigo { get; }
    public string Nombre { get; }

    public static bool EsCodigoValido(string? codigo) =>
        !string.IsNullOrWhiteSpace(codigo) && Nombres.ContainsKey(codigo.Trim().ToUpperInvariant());

    public static Pais Of(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            throw new ErroresDeDominioException(new Error(
                "pais.codigo_requerido", "El código de país es obligatorio.", nameof(codigo)));
        }

        var normalizado = codigo.Trim().ToUpperInvariant();

        if (!Nombres.TryGetValue(normalizado, out var nombre))
        {
            throw new ErroresDeDominioException(new Error(
                "pais.codigo_invalido", $"Código de país no reconocido: '{codigo}'.", nameof(codigo)));
        }

        return new Pais(normalizado, nombre);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Codigo;
    }
}
