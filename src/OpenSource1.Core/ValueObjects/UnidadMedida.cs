using OpenSource1.Core.Abstractions;
using OpenSource1.Core.Common;

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

    /// <param name="codigo">Código de unidad de medida.</param>
    /// <param name="nombreCampo">
    /// Nombre del campo tal como lo conoce el llamante (p. ej. <c>nameof(request.UnidadMedidaCodigo)</c>).
    /// Si no se indica se usa <c>nameof(codigo)</c> como respaldo.
    /// </param>
    public static UnidadMedida Of(string codigo, string? nombreCampo = null)
    {
        var campo = nombreCampo ?? nameof(codigo);

        if (string.IsNullOrWhiteSpace(codigo))
        {
            throw new ErroresDeDominioException(new Error(
                "unidad_medida.codigo_requerido", "El código de unidad de medida es obligatorio.", campo));
        }

        var normalizado = codigo.Trim().ToUpperInvariant();

        if (!Nombres.TryGetValue(normalizado, out var nombre))
        {
            throw new ErroresDeDominioException(new Error(
                "unidad_medida.codigo_invalido", $"Código de unidad de medida no reconocido: '{codigo}'.", campo));
        }

        return new UnidadMedida(normalizado, nombre);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Codigo;
    }
}
