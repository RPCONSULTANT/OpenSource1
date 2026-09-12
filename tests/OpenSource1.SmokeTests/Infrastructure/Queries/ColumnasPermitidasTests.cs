using OpenSource1.Infrastructure.Data.Queries;

namespace OpenSource1.SmokeTests.Infrastructure.Queries;

public class ColumnasPermitidasTests
{
    private static readonly ColumnasPermitidas Permitidas = new("Nombre", "Email");

    [Fact]
    public void EsValida_DistingueColumnasConocidas()
    {
        Assert.True(Permitidas.EsValida("Nombre"));
        Assert.False(Permitidas.EsValida("Contrasena"));
    }

    [Fact]
    public void Citar_DevuelveLaColumnaEntreComillas()
    {
        Assert.Equal("\"Nombre\"", Permitidas.Citar("Nombre"));
    }

    [Theory]
    [InlineData("Nombre\"; DROP TABLE \"Clientes")]
    [InlineData("1=1")]
    [InlineData("")]
    public void Citar_RechazaColumnasNoPermitidas(string columna)
    {
        Assert.Throws<ArgumentException>(() => Permitidas.Citar(columna));
    }
}
