using Moq;
using OpenSource1.Application.Features.Busqueda;
using OpenSource1.Application.Features.Busqueda.Dtos;
using OpenSource1.Application.Features.Busqueda.Handlers;
using OpenSource1.Application.Features.Busqueda.Queries;

namespace OpenSource1.SmokeTests.Features.Busqueda;

public sealed class BuscarGlobalQueryHandlerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" a ")]
    public async Task QFueraDeRango_FallaConCampoQ_SinLlamarAlRepositorio(string? q)
    {
        var repositorio = new Mock<IBusquedaGlobalRepository>(MockBehavior.Strict);

        var resultado = await new BuscarGlobalQueryHandler(repositorio.Object).Handle(new BuscarGlobalQuery(q), default);

        Assert.True(resultado.EsFallo);
        Assert.Equal(("busqueda.q_invalida", "q"), (resultado.Errores[0].Codigo, resultado.Errores[0].Campo));
    }

    [Fact]
    public async Task QDeMasDe100Caracteres_Falla()
    {
        var repositorio = new Mock<IBusquedaGlobalRepository>(MockBehavior.Strict);

        var resultado = await new BuscarGlobalQueryHandler(repositorio.Object).Handle(new BuscarGlobalQuery(new string('x', 101)), default);

        Assert.Equal("q", resultado.Errores[0].Campo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public async Task LimiteFueraDeRango_FallaConCampoLimite(int limite)
    {
        var repositorio = new Mock<IBusquedaGlobalRepository>(MockBehavior.Strict);

        var resultado = await new BuscarGlobalQueryHandler(repositorio.Object).Handle(new BuscarGlobalQuery("abc", limite), default);

        Assert.Equal(("busqueda.limite_invalido", "limite"), (resultado.Errores[0].Codigo, resultado.Errores[0].Campo));
    }

    [Fact]
    public async Task QValido_SeRecortaYSeConsultaConElLimite()
    {
        var esperado = new BusquedaGlobalResponse([]);
        var repositorio = new Mock<IBusquedaGlobalRepository>();
        repositorio.Setup(r => r.BuscarAsync("tornillo 50%", 7, It.IsAny<CancellationToken>())).ReturnsAsync(esperado);

        var resultado = await new BuscarGlobalQueryHandler(repositorio.Object).Handle(new BuscarGlobalQuery("  tornillo 50%  ", 7), default);

        Assert.True(resultado.EsExito);
        Assert.Same(esperado, resultado.Valor);
    }
}
