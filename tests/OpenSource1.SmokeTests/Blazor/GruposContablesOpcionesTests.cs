extern alias BlazorApp;
using BlazorApp::OpenSource1.Blazor.Components;
using BlazorApp::OpenSource1.Blazor.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenSource1.Application.Features.GruposClienteContable.Dtos;
using OpenSource1.Application.Features.GruposContables.Dtos;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Carga de las opciones de grupos contables de las fichas de producto y cliente (Task 5.3).</summary>
public sealed class GruposContablesOpcionesTests
{
    private static GrupoContableResponse Grupo(Guid id, string codigo) => new() { Id = id, Codigo = codigo, Descripcion = codigo };

    [Fact]
    public async Task Producto_ConCargaCorrecta_TraeLosTresTipos_YPreseleccionaLaSemillaSiEsta()
    {
        var client = new Mock<IGrupoContableApiClient>();
        client.Setup(c => c.ListAllAsync(TipoGrupoContable.Producto, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Grupo(GrupoContableIds.ProductoBienes, "BIENES"), Grupo(Guid.NewGuid(), "SERVICIOS")]);
        client.Setup(c => c.ListAllAsync(TipoGrupoContable.IvaProducto, It.IsAny<CancellationToken>())).ReturnsAsync([Grupo(Guid.NewGuid(), "EXENTO")]);
        client.Setup(c => c.ListAllAsync(TipoGrupoContable.Inventario, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var opciones = await GruposProductoOpciones.CargarAsync(client.Object, NullLogger.Instance);

        Assert.False(opciones.CargaFallida);
        Assert.Equal(["BIENES", "SERVICIOS"], opciones.Producto.Select(o => o.Codigo));
        Assert.Single(opciones.IvaProducto);
        Assert.Empty(opciones.Inventario);
        Assert.Equal(GrupoContableIds.ProductoBienes, GruposOpciones.PorDefecto(opciones.Producto, GrupoContableIds.ProductoBienes));
        // La semilla no está en la lista (borrada o no cargada): no se preselecciona nada.
        Assert.Null(GruposOpciones.PorDefecto(opciones.IvaProducto, GrupoContableIds.IvaProductoItbis18));
    }

    [Fact]
    public async Task Producto_SiUnTipoFalla_MarcaCargaFallida_SinPropagarLaExcepcion()
    {
        var client = new Mock<IGrupoContableApiClient>();
        client.Setup(c => c.ListAllAsync(It.IsAny<TipoGrupoContable>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        client.Setup(c => c.ListAllAsync(TipoGrupoContable.Inventario, It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("500"));

        var opciones = await GruposProductoOpciones.CargarAsync(client.Object, NullLogger.Instance);

        Assert.True(opciones.CargaFallida);
        Assert.Empty(opciones.Producto);
    }

    [Fact]
    public async Task Socio_IncluyeLosGruposDeCliente_YSiFallanMarcaCargaFallida()
    {
        var grupos = new Mock<IGrupoContableApiClient>();
        grupos.Setup(c => c.ListAllAsync(It.IsAny<TipoGrupoContable>(), It.IsAny<CancellationToken>())).ReturnsAsync([Grupo(Guid.NewGuid(), "NACIONAL")]);
        var clientes = new Mock<IGrupoClienteContableApiClient>();
        clientes.Setup(c => c.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GrupoClienteContableResponse { Id = GrupoContableIds.ClienteContableGeneral, Codigo = "GENERAL", Descripcion = "General" }]);

        var opciones = await GruposSocioOpciones.CargarAsync(grupos.Object, clientes.Object, NullLogger.Instance);
        Assert.False(opciones.CargaFallida);
        Assert.Equal("GENERAL", Assert.Single(opciones.ClienteContable).Codigo);

        clientes.Setup(c => c.ListAllAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new TaskCanceledException());
        var caida = await GruposSocioOpciones.CargarAsync(grupos.Object, clientes.Object, NullLogger.Instance);
        Assert.True(caida.CargaFallida);
    }
}
