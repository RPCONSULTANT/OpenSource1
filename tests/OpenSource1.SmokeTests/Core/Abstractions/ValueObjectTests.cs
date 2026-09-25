using OpenSource1.Core.Abstractions;

namespace OpenSource1.SmokeTests.Core.Abstractions;

public class ValueObjectTests
{
    private sealed class Codigo(string valor) : ValueObject
    {
        public string Valor { get; } = valor;
        protected override IEnumerable<object?> GetEqualityComponents() { yield return Valor; }
    }

    private sealed class Nombre(string valor) : ValueObject
    {
        public string Valor { get; } = valor;
        protected override IEnumerable<object?> GetEqualityComponents() { yield return Valor; }
    }

    [Fact]
    public void TiposDistintos_ConMismoComponente_NoSonIguales()
    {
        Assert.NotEqual<object>(new Codigo("KG"), new Nombre("KG"));
    }

    [Fact]
    public void MismoTipo_ConMismoComponente_SonIguales()
    {
        Assert.Equal(new Codigo("KG"), new Codigo("KG"));
    }

    [Fact]
    public void OperadorIgualdad_FuncionaConNulos()
    {
        Codigo? nulo = null;
        Assert.True(nulo == null);
        Assert.False(new Codigo("KG") == null);
    }

    [Fact]
    public void OperadorDesigualdad_FuncionaCorrectamente()
    {
        Assert.True(new Codigo("KG") != new Nombre("KG"));
        Assert.True(new Codigo("KG") != new Codigo("L"));
        Assert.False(new Codigo("KG") != new Codigo("KG"));
        Codigo? nulo = null;
        Assert.True(nulo != new Codigo("KG"));
        Assert.False(nulo != null);
    }

    [Fact]
    public void IgualesValoresGeneranMismoHashCode()
    {
        var vo1 = new Codigo("KG");
        var vo2 = new Codigo("KG");

        Assert.Equal(vo1.GetHashCode(), vo2.GetHashCode());
    }

    [Fact]
    public void ValueObjectFuncionaComoClaveEnDictionary()
    {
        var dict = new Dictionary<Codigo, string>
        {
            { new Codigo("KG"), "Kilogramo" }
        };

        Assert.True(dict.ContainsKey(new Codigo("KG")));
        Assert.Equal("Kilogramo", dict[new Codigo("KG")]);
    }
}
