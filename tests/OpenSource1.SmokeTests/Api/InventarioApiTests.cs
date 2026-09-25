using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Entities;
using OpenSource1.Infrastructure.Data;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Api;

/// <summary>
/// <c>POST api/inventario/ajustar-costo</c> (Task 3.5), política <c>CanModify</c>: en este sistema la tienen
/// Administrador y Supervisor (no Ejecutor), igual que postear/aplicar en <c>PermisosPorRol</c>.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class InventarioApiTests : IClassFixture<PostgresTestFixture>
{
    private static readonly Guid UnidadUnd = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid CategoriaGeneral = Guid.Parse("c1000000-0000-0000-0000-000000000001");

    private readonly PostgresTestFixture _fixture;
    private readonly HttpClient _client;

    public InventarioApiTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateFactory().CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task AjustarCosto_ConCanModify200_SinCanModify403_Anonimo401()
    {
        var producto = await SembrarProductoPendienteAsync();

        var anonimo = new HttpRequestMessage(HttpMethod.Post, $"/api/inventario/ajustar-costo?productoId={producto}");
        anonimo.Headers.Add("X-Test-Anonymous", "true");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(anonimo)).StatusCode);

        var ejecutor = await Enviar("Ejecutor", $"/api/inventario/ajustar-costo?productoId={producto}");
        Assert.Equal(HttpStatusCode.Forbidden, ejecutor.StatusCode);
        Assert.False(await CostoAjustadoAsync(producto));

        var supervisor = await Enviar("Supervisor", $"/api/inventario/ajustar-costo?productoId={producto}");
        Assert.Equal(HttpStatusCode.OK, supervisor.StatusCode);
        var cuerpo = await supervisor.Content.ReadFromJsonAsync<Dictionary<string, int>>();
        Assert.Equal(new Dictionary<string, int> { ["productosAjustados"] = 1, ["movimientosValorCreados"] = 0 }, cuerpo);
        Assert.True(await CostoAjustadoAsync(producto));

        var global = await Enviar("Administrador", "/api/inventario/ajustar-costo");
        Assert.Equal(HttpStatusCode.OK, global.StatusCode);
        // Sin productoId: recorre los pendientes de la base (el ya ajustado no vuelve a contarse; ninguno tiene movimientos).
        Assert.Equal(0, (await global.Content.ReadFromJsonAsync<ResultadoAjusteCosto>())!.MovimientosValorCreados);
    }

    [Fact]
    public async Task AjustarCosto_ProductoInexistente_400()
    {
        var respuesta = await Enviar("Administrador", $"/api/inventario/ajustar-costo?productoId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    private async Task<HttpResponseMessage> Enviar(string rol, string url)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Post, url);
        peticion.Headers.Add("X-Test-User", rol.ToLowerInvariant());
        peticion.Headers.Add("X-Test-Roles", rol);
        return await _client.SendAsync(peticion);
    }

    private ApplicationDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_fixture.AppConnectionString).Options);

    private async Task<Guid> SembrarProductoPendienteAsync()
    {
        await using var contexto = NuevoContexto();
        var producto = new Producto
        {
            Codigo = $"AJ{Guid.NewGuid():N}"[..20],
            Nombre = "Producto pendiente de ajuste",
            PrecioVenta = 1m,
            Stock = 0,
            CategoriaId = CategoriaGeneral,
            UnidadMedidaBaseId = UnidadUnd,
            CostoUnitario = 5m,
            CostoAjustado = false,
            CreatedBy = "test",
        };
        contexto.Productos.Add(producto);
        await contexto.SaveChangesAsync();
        return producto.Id;
    }

    private async Task<bool> CostoAjustadoAsync(Guid productoId)
    {
        await using var contexto = NuevoContexto();
        return await contexto.Productos.IgnoreQueryFilters().Where(p => p.Id == productoId)
            .Select(p => p.CostoAjustado).SingleAsync();
    }
}
