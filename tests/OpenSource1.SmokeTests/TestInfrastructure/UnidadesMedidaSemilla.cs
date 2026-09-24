namespace OpenSource1.SmokeTests.TestInfrastructure;

/// <summary>Catálogo que la migración AddUnidadesMedida debe sembrar (Codigo, Nombre, Decimales).</summary>
internal static class UnidadesMedidaSemilla
{
    public static readonly IReadOnlyList<(string Codigo, string Nombre, short Decimales)> Catalogo =
    [
        ("UND", "Unidad", 0),
        ("KG", "Kilogramo", 3),
        ("GR", "Gramo", 0),
        ("LT", "Litro", 3),
        ("ML", "Mililitro", 0),
        ("CJA", "Caja", 0),
        ("DOC", "Docena", 0),
        ("PAQ", "Paquete", 0),
        ("MT", "Metro", 2),
        ("LB", "Libra", 3),
    ];
}
