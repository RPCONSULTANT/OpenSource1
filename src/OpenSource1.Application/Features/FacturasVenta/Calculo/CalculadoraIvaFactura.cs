namespace OpenSource1.Application.Features.FacturasVenta.Calculo;

/// <summary>Línea que entra al cálculo del IVA (las de tipo Comentario no entran).</summary>
public sealed record LineaCalculoIva(int NumeroLinea, string IdentificadorIva, decimal PorcentajeIva, decimal ImporteLinea);

/// <summary>Un grupo de IVA: la base y el IVA se redondean a 2 por GRUPO, no por línea (spec 6.3).</summary>
public sealed record GrupoIvaCalculado(string IdentificadorIva, decimal PorcentajeIva, decimal BaseImponible, decimal ImporteIva);

/// <summary>Totales de la factura: <c>ImporteSinIva = Σ bases</c>, <c>ImporteIva = Σ IVA de grupos</c>, <c>ImporteTotal</c> = suma de ambos.</summary>
public sealed record TotalesFactura(IReadOnlyList<GrupoIvaCalculado> Grupos, decimal ImporteSinIva, decimal ImporteIva, decimal ImporteTotal);

/// <summary>
/// Cálculo del IVA agrupado (spec 6.3), puro y sin BD. La Task 6.2 lo usa para la vista previa de totales de un borrador; la
/// Task 6.3 lo completa y prueba exhaustivamente, y la 6.4 lo usa al postear.
/// </summary>
public static class CalculadoraIvaFactura
{
    /// <summary>
    /// Agrupa por <see cref="LineaCalculoIva.IdentificadorIva"/> (comparación ordinal); por grupo,
    /// <c>base = ROUND(Σ ImporteLinea, 2)</c> e <c>IVA = ROUND(base × % / 100, 2)</c>, ambos <see cref="MidpointRounding.AwayFromZero"/>.
    /// Los grupos salen ordenados por identificador (ordinal). Un mismo identificador con porcentajes distintos lanza
    /// <see cref="InvalidOperationException"/> (no debería ocurrir: el identificador es la clave del setup de IVA).
    /// </summary>
    public static TotalesFactura Calcular(IReadOnlyList<LineaCalculoIva> lineas)
    {
        ArgumentNullException.ThrowIfNull(lineas);

        var grupos = new List<GrupoIvaCalculado>();
        foreach (var grupo in lineas.GroupBy(l => l.IdentificadorIva, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var porcentajes = grupo.Select(l => l.PorcentajeIva).Distinct().ToList();
            if (porcentajes.Count > 1)
            {
                throw new InvalidOperationException(
                    $"El identificador de IVA '{grupo.Key}' aparece con porcentajes distintos ({string.Join(", ", porcentajes)}) " +
                    $"en las líneas {string.Join(", ", grupo.Select(l => l.NumeroLinea))}.");
            }

            var porcentaje = porcentajes[0];
            var baseImponible = Redondear(grupo.Sum(l => l.ImporteLinea));
            var importeIva = Redondear(baseImponible * porcentaje / 100m);
            grupos.Add(new GrupoIvaCalculado(grupo.Key, porcentaje, baseImponible, importeIva));
        }

        var importeSinIva = grupos.Sum(g => g.BaseImponible);
        var importeIvaTotal = grupos.Sum(g => g.ImporteIva);
        return new TotalesFactura(grupos, importeSinIva, importeIvaTotal, importeSinIva + importeIvaTotal);
    }

    private static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
