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
}
