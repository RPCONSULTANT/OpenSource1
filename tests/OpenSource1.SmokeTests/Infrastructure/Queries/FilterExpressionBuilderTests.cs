using Dapper;
using OpenSource1.Infrastructure.Data.Queries;

namespace OpenSource1.SmokeTests.Infrastructure.Queries;

public class FilterExpressionBuilderTests
{
    private static readonly ColumnasPermitidas Permitidas = new("Nombre", "Precio");

    [Fact]
    public void AddTextFilter_ColumnaNoPermitida_Lanza()
    {
        var filtros = new List<string>();
        var parametros = new DynamicParameters();

        Assert.Throws<ArgumentException>(() =>
            FilterExpressionBuilder.AddTextFilter(filtros, parametros, Permitidas, "Inyeccion", "x"));
    }

    [Fact]
    public void AddExactFilter_ValorInvalido_DevuelveFallo()
    {
        var filtros = new List<string>();
        var parametros = new DynamicParameters();

        var resultado = FilterExpressionBuilder.AddExactFilter<decimal>(
            filtros, parametros, Permitidas, "Precio", "abc",
            t => (decimal.TryParse(t, out var v), v));

        Assert.False(resultado.EsExito);
        Assert.Equal("filtro.valor_invalido", resultado.Errores[0].Codigo);
        Assert.Empty(filtros);
    }

    [Fact]
    public void AddExactFilter_ValorValido_AgregaClausula()
    {
        var filtros = new List<string>();
        var parametros = new DynamicParameters();

        var resultado = FilterExpressionBuilder.AddExactFilter<decimal>(
            filtros, parametros, Permitidas, "Precio", "10.50",
            t => (decimal.TryParse(t, System.Globalization.CultureInfo.InvariantCulture, out var v), v));

        Assert.True(resultado.EsExito);
        Assert.Single(filtros);
    }

    [Fact]
    public void AddTextFilter_ColumnaMaliciosa_NuncaLlegaAlSqlGenerado()
    {
        // Evidencia a nivel del builder (no solo de ColumnasPermitidas.Citar): un nombre
        // de columna hostil no debe producir cláusula SQL alguna ni aparecer en ella.
        const string columnaMaliciosa = "Nombre\"; DROP TABLE \"Clientes";
        var filtros = new List<string>();
        var parametros = new DynamicParameters();

        var excepcion = Assert.Throws<ArgumentException>(() =>
            FilterExpressionBuilder.AddTextFilter(filtros, parametros, Permitidas, columnaMaliciosa, "x"));

        Assert.Empty(filtros);
        Assert.DoesNotContain("DROP TABLE", excepcion.Message.Replace(columnaMaliciosa, string.Empty));
    }

    [Fact]
    public void AddExactFilter_ColumnaMaliciosa_NuncaLlegaAlSqlGenerado()
    {
        const string columnaMaliciosa = "Precio\"; DROP TABLE \"Productos";
        var filtros = new List<string>();
        var parametros = new DynamicParameters();

        Assert.Throws<ArgumentException>(() =>
            FilterExpressionBuilder.AddExactFilter<decimal>(
                filtros, parametros, Permitidas, columnaMaliciosa, "1",
                t => (decimal.TryParse(t, out var v), v)));

        Assert.Empty(filtros);
    }
}
