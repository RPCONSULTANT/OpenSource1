using OpenSource1.Core.Common;

namespace OpenSource1.SmokeTests.Core.Common;

public class PageRequestTests
{
    [Fact]
    public void Normalizar_CorrigePaginaMenorQueUno()
    {
        Assert.Equal(1, new PageRequest(0, 50).Normalizar().Pagina);
        Assert.Equal(1, new PageRequest(-5, 50).Normalizar().Pagina);
    }

    [Fact]
    public void Normalizar_LimitaElTamanoMaximo()
    {
        Assert.Equal(PageRequest.TamanoMaximo, new PageRequest(1, 5_000).Normalizar().TamanoPagina);
    }

    [Fact]
    public void Normalizar_CorrigeTamanoMenorQueUno()
    {
        Assert.Equal(PageRequest.TamanoPorDefecto, new PageRequest(1, 0).Normalizar().TamanoPagina);
    }

    [Fact]
    public void Offset_SeCalculaDesdeLaPaginaNormalizada()
    {
        Assert.Equal(0, new PageRequest(1, 20).Normalizar().Offset);
        Assert.Equal(40, new PageRequest(3, 20).Normalizar().Offset);
    }

    [Fact]
    public void TotalPaginas_RedondeaHaciaArriba()
    {
        var pagina = new PagedResult<int>([1, 2], 1, 20, 41);
        Assert.Equal(3, pagina.TotalPaginas);
    }

    [Fact]
    public void TotalPaginas_ConCeroElementos_EsCero()
    {
        Assert.Equal(0, new PagedResult<int>([], 1, 20, 0).TotalPaginas);
    }
}
