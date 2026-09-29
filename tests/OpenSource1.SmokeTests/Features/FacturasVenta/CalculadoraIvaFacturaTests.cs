using OpenSource1.Application.Features.FacturasVenta.Calculo;

namespace OpenSource1.SmokeTests.Features.FacturasVenta;

/// <summary>
/// Calculadora del IVA agrupado (spec 6.3, Review Focus 1). Pura, sin BD. Por grupo de <c>IdentificadorIva</c>:
/// <c>base = ROUND(Σ ImporteLinea, 2)</c>, <c>IVA = ROUND(base × % / 100, 2)</c> (AwayFromZero); los totales son la suma de los
/// grupos. Los casos con números concretos demuestran que el IVA por línea NO es el del documento.
/// </summary>
public class CalculadoraIvaFacturaTests
{
    private static decimal IvaPorLinea(IEnumerable<LineaCalculoIva> lineas) =>
        lineas.Sum(l => Math.Round(l.ImporteLinea * l.PorcentajeIva / 100m, 2, MidpointRounding.AwayFromZero));

    // ----- Review Focus 1: agrupado frente a por línea -----

    [Fact]
    public void TresLineasDe10_03Al18_ElGrupoDa5_42YNo5_43()
    {
        LineaCalculoIva[] lineas =
        [
            new(10000, "ITBIS18", 18m, 10.03m),
            new(20000, "ITBIS18", 18m, 10.03m),
            new(30000, "ITBIS18", 18m, 10.03m),
        ];

        var totales = CalculadoraIvaFactura.Calcular(lineas);

        // Por línea: ROUND(10.03 × 0.18 = 1.8054, 2) = 1.81 → 5.43. Agrupado: ROUND(30.09 × 0.18 = 5.4162, 2) = 5.42.
        Assert.Equal(5.43m, IvaPorLinea(lineas));
        var grupo = Assert.Single(totales.Grupos);
        Assert.Equal(new GrupoIvaCalculado("ITBIS18", 18m, 30.09m, 5.42m), grupo);
        Assert.Equal((30.09m, 5.42m, 35.51m), (totales.ImporteSinIva, totales.ImporteIva, totales.ImporteTotal));
    }

    [Fact]
    public void TresLineasDe0_33Al18_CoincideConElPorLinea()
    {
        LineaCalculoIva[] lineas =
        [
            new(10000, "ITBIS18", 18m, 0.33m),
            new(20000, "ITBIS18", 18m, 0.33m),
            new(30000, "ITBIS18", 18m, 0.33m),
        ];

        var totales = CalculadoraIvaFactura.Calcular(lineas);

        // Por línea 0.0594 → 0.06 × 3 = 0.18; agrupado ROUND(0.99 × 0.18 = 0.1782, 2) = 0.18.
        Assert.Equal(0.18m, IvaPorLinea(lineas));
        Assert.Equal(new GrupoIvaCalculado("ITBIS18", 18m, 0.99m, 0.18m), Assert.Single(totales.Grupos));
        Assert.Equal((0.99m, 0.18m, 1.17m), (totales.ImporteSinIva, totales.ImporteIva, totales.ImporteTotal));
    }

    [Fact]
    public void ElAgrupadoPuedeSerMayorQueElPorLinea()
    {
        // Por línea: ROUND(0.02 × 0.18 = 0.0036, 2) = 0.00 × 3 = 0.00. Agrupado: ROUND(0.06 × 0.18 = 0.0108, 2) = 0.01.
        LineaCalculoIva[] lineas =
        [
            new(10000, "ITBIS18", 18m, 0.02m),
            new(20000, "ITBIS18", 18m, 0.02m),
            new(30000, "ITBIS18", 18m, 0.02m),
        ];

        var totales = CalculadoraIvaFactura.Calcular(lineas);

        Assert.Equal(0m, IvaPorLinea(lineas));
        Assert.Equal(0.01m, totales.ImporteIva);
        Assert.Equal(0.07m, totales.ImporteTotal);
    }

    // ----- Redondeo -----

    [Fact]
    public void ElPuntoMedioRedondeaAlejandoseDeCero()
    {
        // 0.25 × 18 % = 0.045 → AwayFromZero 0.05 (el redondeo bancario daría 0.04).
        var totales = CalculadoraIvaFactura.Calcular([new(10000, "ITBIS18", 18m, 0.25m)]);

        Assert.Equal(0.05m, totales.ImporteIva);
    }

    [Fact]
    public void LaBaseSeRedondeaA2AntesDeCalcularElIva()
    {
        // Importes de 4 decimales: A = 0.0049 + 0.0049 = 0.0098 → base 0.01 → IVA ROUND(0.005) = 0.01;
        // B = 0.125 → base 0.13 (punto medio) → IVA ROUND(0.0208) = 0.02.
        var totales = CalculadoraIvaFactura.Calcular(
        [
            new(10000, "A", 50m, 0.0049m),
            new(20000, "A", 50m, 0.0049m),
            new(30000, "B", 16m, 0.125m),
        ]);

        Assert.Equal(
            [new GrupoIvaCalculado("A", 50m, 0.01m, 0.01m), new GrupoIvaCalculado("B", 16m, 0.13m, 0.02m)],
            totales.Grupos);
        Assert.Equal((0.14m, 0.03m, 0.17m), (totales.ImporteSinIva, totales.ImporteIva, totales.ImporteTotal));
    }

    [Fact]
    public void ImportesNegativos_RedondeanSimetricos()
    {
        // Una línea negativa (p. ej. un ajuste) resta del grupo; el punto medio negativo se aleja de cero: -0.045 → -0.05.
        var totales = CalculadoraIvaFactura.Calcular(
        [
            new(10000, "ITBIS18", 18m, 10m),
            new(20000, "ITBIS18", 18m, -10.25m),
        ]);

        Assert.Equal(new GrupoIvaCalculado("ITBIS18", 18m, -0.25m, -0.05m), Assert.Single(totales.Grupos));
        Assert.Equal((-0.25m, -0.05m, -0.30m), (totales.ImporteSinIva, totales.ImporteIva, totales.ImporteTotal));
    }

    [Fact]
    public void PorcentajeConDecimales()
    {
        // 1000 × 16.5 % = 165; 33.33 × 7.12345 % = 2.374245885 → 2.37.
        var totales = CalculadoraIvaFactura.Calcular(
        [
            new(10000, "IVA16_5", 16.5m, 1000m),
            new(20000, "RARO", 7.12345m, 33.33m),
        ]);

        Assert.Equal(
            [new GrupoIvaCalculado("IVA16_5", 16.5m, 1000m, 165m), new GrupoIvaCalculado("RARO", 7.12345m, 33.33m, 2.37m)],
            totales.Grupos);
    }

    // ----- Exento, varios identificadores, orden -----

    [Fact]
    public void Exento_TieneBaseYCeroDeIva()
    {
        var totales = CalculadoraIvaFactura.Calcular(
        [
            new(10000, "EXENTO", 0m, 100m),
            new(20000, "EXENTO", 0m, 0.01m),
        ]);

        Assert.Equal(new GrupoIvaCalculado("EXENTO", 0m, 100.01m, 0m), Assert.Single(totales.Grupos));
        Assert.Equal((100.01m, 0m, 100.01m), (totales.ImporteSinIva, totales.ImporteIva, totales.ImporteTotal));
    }

    [Fact]
    public void VariosIdentificadores_UnGrupoPorIdentificador_OrdenOrdinal_YTotalesSumaDeGrupos()
    {
        // Líneas intercaladas: el agrupado no depende del orden de entrada.
        LineaCalculoIva[] lineas =
        [
            new(10000, "ITBIS18", 18m, 10.03m),
            new(20000, "ITBIS16", 16m, 0.31m),
            new(30000, "EXENTO", 0m, 50m),
            new(40000, "ITBIS18", 18m, 10.03m),
            new(50000, "ITBIS16", 16m, 0.31m),
            new(60000, "ITBIS18", 18m, 10.03m),
            new(70000, "ITBIS16", 16m, 0.31m),
        ];

        var totales = CalculadoraIvaFactura.Calcular(lineas);

        // ITBIS16: por línea ROUND(0.0496) = 0.05 × 3 = 0.15; agrupado ROUND(0.93 × 0.16 = 0.1488) = 0.15.
        Assert.Equal(
        [
            new GrupoIvaCalculado("EXENTO", 0m, 50m, 0m),
            new GrupoIvaCalculado("ITBIS16", 16m, 0.93m, 0.15m),
            new GrupoIvaCalculado("ITBIS18", 18m, 30.09m, 5.42m),
        ], totales.Grupos);
        Assert.Equal(totales.Grupos.Sum(g => g.BaseImponible), totales.ImporteSinIva);
        Assert.Equal(totales.Grupos.Sum(g => g.ImporteIva), totales.ImporteIva);
        Assert.Equal((81.02m, 5.57m, 86.59m), (totales.ImporteSinIva, totales.ImporteIva, totales.ImporteTotal));
        Assert.Equal(5.58m, IvaPorLinea(lineas)); // el documento NO usa este valor

        var invertidas = CalculadoraIvaFactura.Calcular(lineas.Reverse().ToList());
        Assert.Equal(totales.Grupos, invertidas.Grupos);
        Assert.Equal((totales.ImporteSinIva, totales.ImporteIva, totales.ImporteTotal),
            (invertidas.ImporteSinIva, invertidas.ImporteIva, invertidas.ImporteTotal));
    }

    [Fact]
    public void ElIdentificadorSeComparaOrdinal_MayusculasDistinguen()
    {
        var totales = CalculadoraIvaFactura.Calcular(
        [
            new(10000, "itbis18", 18m, 1m),
            new(20000, "ITBIS18", 18m, 1m),
        ]);

        // Ordinal: 'I' (0x49) < 'i' (0x69).
        Assert.Equal(["ITBIS18", "itbis18"], totales.Grupos.Select(g => g.IdentificadorIva));
    }

    [Fact]
    public void MismoPorcentajeConDistintaEscala_EsElMismoGrupo()
    {
        // numeric(9,5) llega como 18.00000: es el mismo porcentaje que 18.
        var totales = CalculadoraIvaFactura.Calcular(
        [
            new(10000, "ITBIS18", 18m, 1m),
            new(20000, "ITBIS18", 18.00000m, 1m),
        ]);

        var grupo = Assert.Single(totales.Grupos);
        Assert.Equal((2m, 0.36m), (grupo.BaseImponible, grupo.ImporteIva));
    }

    [Fact]
    public void UnaSolaLinea()
    {
        var totales = CalculadoraIvaFactura.Calcular([new(10000, "ITBIS18", 18m, 1234.56m)]);

        // 1234.56 × 0.18 = 222.2208 → 222.22.
        Assert.Equal(new GrupoIvaCalculado("ITBIS18", 18m, 1234.56m, 222.22m), Assert.Single(totales.Grupos));
        Assert.Equal(1456.78m, totales.ImporteTotal);
    }

    [Fact]
    public void LineaConImporteCero_CreaElGrupoConCeros()
    {
        var totales = CalculadoraIvaFactura.Calcular([new(10000, "ITBIS18", 18m, 0m)]);

        Assert.Equal(new GrupoIvaCalculado("ITBIS18", 18m, 0m, 0m), Assert.Single(totales.Grupos));
        Assert.Equal((0m, 0m, 0m), (totales.ImporteSinIva, totales.ImporteIva, totales.ImporteTotal));
    }

    // ----- Entradas inválidas y vacías -----

    [Fact]
    public void ListaVacia_DevuelveCeros()
    {
        var totales = CalculadoraIvaFactura.Calcular([]);

        Assert.Empty(totales.Grupos);
        Assert.Equal((0m, 0m, 0m), (totales.ImporteSinIva, totales.ImporteIva, totales.ImporteTotal));
    }

    [Fact]
    public void ListaNula_Lanza() =>
        Assert.Throws<ArgumentNullException>(() => CalculadoraIvaFactura.Calcular(null!));

    [Fact]
    public void MismoIdentificadorConPorcentajesDistintos_LanzaConLasLineas()
    {
        var error = Assert.Throws<InvalidOperationException>(() => CalculadoraIvaFactura.Calcular(
        [
            new(10000, "ITBIS18", 18m, 1m),
            new(20000, "EXENTO", 0m, 1m),
            new(30000, "ITBIS18", 16m, 1m),
        ]));

        Assert.Contains("ITBIS18", error.Message);
        Assert.Contains("10000", error.Message);
        Assert.Contains("30000", error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IdentificadorVacio_Lanza(string? identificador)
    {
        var error = Assert.Throws<ArgumentException>(() => CalculadoraIvaFactura.Calcular(
            [new LineaCalculoIva(20000, identificador!, 18m, 1m)]));

        Assert.Contains("20000", error.Message);
        // El mensaje llega al usuario (vista previa de totales): sin el sufijo " (Parameter 'lineas')" de ArgumentException.
        Assert.Null(error.ParamName);
        Assert.DoesNotContain("Parameter", error.Message);
    }
}
