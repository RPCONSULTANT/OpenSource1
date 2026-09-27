using OpenSource1.Application.Features.FechasRegistro;

namespace OpenSource1.SmokeTests.Features.FechasRegistro;

/// <summary>Semántica del rango de fechas de registro permitidas (Task 8.5): límites inclusivos, null = sin límite.</summary>
public sealed class RangoFechasRegistroTests
{
    private static readonly DateOnly D1 = new(2026, 9, 1);
    private static readonly DateOnly D10 = new(2026, 9, 10);
    private static readonly DateOnly D20 = new(2026, 9, 20);

    [Theory]
    [InlineData(null, null, "2026-01-01", true)]
    [InlineData("2026-09-10", null, "2026-09-09", false)]
    [InlineData("2026-09-10", null, "2026-09-10", true)]
    [InlineData(null, "2026-09-10", "2026-09-10", true)]
    [InlineData(null, "2026-09-10", "2026-09-11", false)]
    [InlineData("2026-09-01", "2026-09-20", "2026-09-01", true)]
    [InlineData("2026-09-01", "2026-09-20", "2026-09-20", true)]
    [InlineData("2026-09-01", "2026-09-20", "2026-08-31", false)]
    [InlineData("2026-09-01", "2026-09-20", "2026-09-21", false)]
    public void Permite_LimitesInclusivos_YNullSinLimite(string? desde, string? hasta, string fecha, bool esperado)
    {
        var rango = new RangoFechasRegistro(Parse(desde), Parse(hasta));

        Assert.Equal(esperado, rango.Permite(DateOnly.Parse(fecha, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Describir_ConLosLimitesVigentes()
    {
        Assert.Equal("del 01/09/2026 al 20/09/2026", new RangoFechasRegistro(D1, D20).Describir());
        Assert.Equal("desde el 10/09/2026", new RangoFechasRegistro(D10, null).Describir());
        Assert.Equal("hasta el 10/09/2026", new RangoFechasRegistro(null, D10).Describir());
        Assert.Equal("sin límites", new RangoFechasRegistro(null, null).Describir());
    }

    [Fact]
    public void ValidarLimites_DesdePosteriorAHasta_Error_YElMismoDiaOUnLadoAbiertoNo()
    {
        var error = RangoFechasRegistro.ValidarLimites(D20, D10);

        Assert.NotNull(error);
        Assert.Equal(("registro.rango_invalido", "PermitirRegistroHasta"), (error.Value.Codigo, error.Value.Campo));
        Assert.Null(RangoFechasRegistro.ValidarLimites(D10, D10));
        Assert.Null(RangoFechasRegistro.ValidarLimites(D10, null));
        Assert.Null(RangoFechasRegistro.ValidarLimites(null, D10));
        Assert.Null(RangoFechasRegistro.ValidarLimites(null, null));
    }

    private static DateOnly? Parse(string? texto) => texto is null ? null : DateOnly.Parse(texto, System.Globalization.CultureInfo.InvariantCulture);
}
