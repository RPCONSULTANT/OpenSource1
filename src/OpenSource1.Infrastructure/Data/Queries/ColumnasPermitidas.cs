using System.Collections.Frozen;

namespace OpenSource1.Infrastructure.Data.Queries;

/// <summary>
/// Lista blanca de columnas que pueden interpolarse en SQL. Cierra el sink de inyección
/// que existía al construir cláusulas con el nombre de columna sin validar.
/// </summary>
internal sealed class ColumnasPermitidas
{
    private readonly FrozenSet<string> _columnas;

    public ColumnasPermitidas(params string[] columnas) =>
        _columnas = columnas.ToFrozenSet(StringComparer.Ordinal);

    public bool EsValida(string? columna) =>
        !string.IsNullOrWhiteSpace(columna) && _columnas.Contains(columna);

    public string Citar(string columna)
    {
        if (!EsValida(columna))
        {
            throw new ArgumentException($"Columna no permitida: '{columna}'.", nameof(columna));
        }

        return $"\"{columna}\"";
    }
}
