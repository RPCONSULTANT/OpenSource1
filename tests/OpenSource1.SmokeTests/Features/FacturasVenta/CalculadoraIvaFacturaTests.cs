using OpenSource1.Application.Features.FacturasVenta.Calculo;

namespace OpenSource1.SmokeTests.Features.FacturasVenta;

/// <summary>
/// Prueba de humo de la calculadora (Task 6.2 la crea para la vista previa de totales; la Task 6.3 añade los tests exhaustivos).
/// </summary>
public class CalculadoraIvaFacturaTests
{
    [Fact]
    public void Calcular_AgrupaPorIdentificador_YRedondeaPorGrupoNoPorLinea()
    {
        // 3 × 10.03 al 18 %: por línea sería 1.81 × 3 = 5.43; agrupado ROUND(30.09 × 0.18, 2) = 5.42.
        var totales = CalculadoraIvaFactura.Calcular(
        [
            new LineaCalculoIva(10000, "ITBIS18", 18m, 10.03m),
            new LineaCalculoIva(20000, "EXENTO", 0m, 50m),
            new LineaCalculoIva(30000, "ITBIS18", 18m, 10.03m),
            new LineaCalculoIva(40000, "ITBIS18", 18m, 10.03m),
        ]);

        Assert.Equal(
            [new GrupoIvaCalculado("EXENTO", 0m, 50m, 0m), new GrupoIvaCalculado("ITBIS18", 18m, 30.09m, 5.42m)],
            totales.Grupos);
        Assert.Equal((80.09m, 5.42m, 85.51m), (totales.ImporteSinIva, totales.ImporteIva, totales.ImporteTotal));
    }

    [Fact]
    public void Calcular_ListaVacia_DevuelveCeros()
    {
        var totales = CalculadoraIvaFactura.Calcular([]);

        Assert.Empty(totales.Grupos);
        Assert.Equal((0m, 0m, 0m), (totales.ImporteSinIva, totales.ImporteIva, totales.ImporteTotal));
    }

    [Fact]
    public void Calcular_MismoIdentificadorConPorcentajesDistintos_Lanza()
    {
        Assert.Throws<InvalidOperationException>(() => CalculadoraIvaFactura.Calcular(
        [
            new LineaCalculoIva(10000, "ITBIS18", 18m, 1m),
            new LineaCalculoIva(20000, "ITBIS18", 16m, 1m),
        ]));
    }
}
