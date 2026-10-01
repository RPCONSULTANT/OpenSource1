using System.Text.RegularExpressions;
using OpenSource1.Application.Features.Series;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.DiariosInventario.Lotes;

/// <summary>
/// Validación pura (sin BD) compartida entre Create/Update de <c>LoteDiario</c>, mismo patrón que
/// <c>AlmacenValidator</c>: el código se normaliza a mayúsculas y su unicidad (junto con
/// <c>PlantillaDiarioId</c>) la garantiza el índice único parcial de Postgres (23505 -> 409).
/// </summary>
internal static partial class LoteDiarioValidator
{
    public static IReadOnlyList<Error> Validar(string codigo, string nombre)
    {
        var errores = new List<Error>();

        if (string.IsNullOrWhiteSpace(codigo))
        {
            errores.Add(new Error("diario.lote_codigo_requerido", "El código del lote es obligatorio.", "Codigo"));
        }
        else if (!CodigoValido().IsMatch(NormalizarCodigo(codigo)))
        {
            errores.Add(new Error(
                "diario.lote_codigo_invalido",
                "El código debe tener entre 1 y 20 caracteres, usando solo letras, números, guion y guion bajo.",
                "Codigo"));
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            errores.Add(new Error("diario.lote_nombre_requerido", "El nombre del lote es obligatorio.", "Nombre"));
        }
        else if (nombre.Trim().Length > 100)
        {
            errores.Add(new Error("diario.lote_nombre_invalido", "El nombre no puede superar los 100 caracteres.", "Nombre"));
        }

        return errores;
    }

    public static string NormalizarCodigo(string codigo) => codigo.Trim().ToUpperInvariant();

    /// <summary>
    /// La serie de un lote debe numerar diarios de inventario (<see cref="TipoDocumentoSerie.DiarioInventario"/>) y estar activa
    /// (spec no-series: sustituye a la regla del prefijo <c>DIARIO-</c>).
    /// </summary>
    public static bool EsSerieDeDiarioValida(EstadoSerie? serie) =>
        serie is { Tipo: TipoDocumentoSerie.DiarioInventario, Activa: true };

    public static Error SerieInvalida() => new(
        "diario.serie_invalida", "La serie indicada no existe, no es de diarios de inventario o está inactiva.", "SerieId");

    [GeneratedRegex("^[A-Z0-9_-]{1,20}$")]
    private static partial Regex CodigoValido();
}
