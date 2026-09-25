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

    /// <param name="codigo">Código ISO del país.</param>
    /// <param name="nombreCampo">
    /// Nombre del campo tal como lo conoce el llamante (p. ej. la propiedad del DTO,
    /// <c>nameof(request.PaisCodigo)</c>). El value object no conoce la forma del DTO que lo
    /// invoca, así que si no se indica se usa <c>nameof(codigo)</c> como respaldo.
    /// </param>
    public static Pais Of(string codigo, string? nombreCampo = null)
    {
        var campo = nombreCampo ?? nameof(codigo);

        if (string.IsNullOrWhiteSpace(codigo))
        {
            throw new ErroresDeDominioException(new Error(
                "pais.codigo_requerido", "El código de país es obligatorio.", campo));
        }

        var normalizado = codigo.Trim().ToUpperInvariant();

        if (!Nombres.TryGetValue(normalizado, out var nombre))
        {
            throw new ErroresDeDominioException(new Error(
                "pais.codigo_invalido", $"Código de país no reconocido: '{codigo}'.", campo));
        }

        return new Pais(normalizado, nombre);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Codigo;
    }
}
