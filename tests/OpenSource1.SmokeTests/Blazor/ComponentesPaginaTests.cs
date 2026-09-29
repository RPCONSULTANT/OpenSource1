extern alias BlazorApp;

using BlazorApp::OpenSource1.Blazor.Components;
using BlazorApp::OpenSource1.Blazor.Navigation;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OpenSource1.Application.Security;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Blazor;

/// <summary>Fix-Features B1: barra de acciones, página-tarjeta, selección y acciones de tarjeta renderizadas con HtmlRenderer.</summary>
public sealed class ComponentesPaginaTests
{
    private static readonly Func<string, string> Editar = id => $"/x/{id}/editar";
    private static readonly Func<string, string> Eliminar = id => $"/x?deleteId={id}";

    [Fact]
    public async Task Toolbar_Admin_SinSeleccion_NuevoVisible_EditarYEliminarDeshabilitados()
    {
        var html = await ToolbarAsync("Administrador", seleccion: null);

        Assert.Contains("data-testid=\"page-toolbar\"", html);
        Assert.Contains("<a href=\"/x/nuevo\" data-testid=\"accion-nuevo\"", html);
        Assert.Contains($"<span data-testid=\"accion-editar\" aria-disabled=\"true\" title=\"{PageToolbar.SinSeleccion}\"", html);
        Assert.Contains("<span data-testid=\"accion-eliminar\" aria-disabled=\"true\"", html);
    }

    [Fact]
    public async Task Toolbar_ConSeleccion_EnlazaConElId()
    {
        var html = await ToolbarAsync("Administrador", seleccion: "abc");

        Assert.Contains("<a data-testid=\"accion-editar\" href=\"/x/abc/editar\"", html);
        Assert.Contains("<a data-testid=\"accion-eliminar\" href=\"/x?deleteId=abc\"", html);
    }

    [Fact]
    public async Task Toolbar_Permisos_EjecutorSinEditarNiEliminar_SupervisorSinNuevo()
    {
        var ejecutor = await ToolbarAsync("Ejecutor", seleccion: "abc");
        var supervisor = await ToolbarAsync("Supervisor", seleccion: "abc");

        Assert.Contains("accion-nuevo", ejecutor);
        Assert.DoesNotContain("accion-editar", ejecutor);
        Assert.DoesNotContain("accion-eliminar", ejecutor);
        Assert.DoesNotContain("accion-nuevo", supervisor);
        Assert.Contains("accion-editar", supervisor);
        Assert.DoesNotContain("accion-eliminar", supervisor);
    }

    [Fact]
    public async Task Toolbar_MenusCrearYVer_FiltranPorPermiso_YDeshabilitanSinSeleccion()
    {
        IReadOnlyList<AccionPagina> crear = [new("Factura", IconosModulo.Documento, id => $"/facturas-venta/nueva?socioId={id}", ApplicationPolicies.CanAdd, RequiereSeleccion: true)];
        IReadOnlyList<AccionPagina> ver =
        [
            new("Ficha", IconosModulo.Persona, id => $"/clientes/{id}", null, RequiereSeleccion: true),
            new("Panel", IconosModulo.Grafico, _ => "/dashboard/clientes"),
        ];

        var adminSinSeleccion = await ToolbarAsync("Administrador", null, crear, ver);
        var adminConSeleccion = await ToolbarAsync("Administrador", "S1", crear, ver);
        var supervisor = await ToolbarAsync("Supervisor", "S1", crear, ver);

        Assert.Contains("data-testid=\"menu-crear\"", adminSinSeleccion);
        Assert.Contains("<span role=\"menuitem\" aria-disabled=\"true\"", adminSinSeleccion);
        Assert.Contains("href=\"/dashboard/clientes\"", adminSinSeleccion);
        Assert.Contains("href=\"/facturas-venta/nueva?socioId=S1\"", adminConSeleccion);
        Assert.Contains("href=\"/clientes/S1\"", adminConSeleccion);
        Assert.DoesNotContain("data-testid=\"menu-crear\"", supervisor);
        Assert.Contains("data-testid=\"menu-ver\"", supervisor);
    }

    [Fact]
    public async Task EntityFormPage_MigasTituloMensaje_YAcciones()
    {
        var html = await RenderAsync<EntityFormPage>(
            PermisosTestAuthHandler.Principal("Administrador"),
            new Dictionary<string, object?>
            {
                ["Titulo"] = "Nueva unidad de medida",
                ["Migas"] = (IReadOnlyList<Miga>)[new("Configuración", "/modulos/configuracion"), new("Nueva")],
                ["Mensaje"] = "El código ya existe.",
                ["TipoMensaje"] = "warning",
            });
        var acciones = await RenderAsync<FormularioAcciones>(
            PermisosTestAuthHandler.Principal("Administrador"),
            new Dictionary<string, object?> { ["CancelarHref"] = "/unidades-medida?pagina=2", ["LimpiarHref"] = "/unidades-medida/nuevo" });

        Assert.Contains("data-testid=\"entity-form-page\"", html);
        Assert.Contains("href=\"/modulos/configuracion\"", html);
        Assert.Contains("Nueva unidad de medida", html);
        Assert.Contains("El código ya existe.", html);
        Assert.Contains("<button type=\"submit\" data-testid=\"guardar\"", acciones);
        Assert.Contains("href=\"/unidades-medida?pagina=2\" data-testid=\"cancelar\"", acciones);
        Assert.Contains("href=\"/unidades-medida/nuevo\" data-testid=\"limpiar\"", acciones);
    }

    [Fact]
    public async Task TarjetaAcciones_DosRapidas_ElRestoEnElMenu_SegunPermiso()
    {
        IReadOnlyList<AccionPagina> rapidas =
        [
            new("Ficha", IconosModulo.Persona, id => $"/clientes/{id}"),
            new("Editar", IconosModulo.Lista, id => $"/clientes/{id}/editar", ApplicationPolicies.CanModify),
            new("Factura", IconosModulo.Documento, id => $"/facturas-venta/nueva?socioId={id}", ApplicationPolicies.CanAdd),
        ];
        IReadOnlyList<AccionPagina> resto = [new("Cobros", IconosModulo.Tarjeta, id => $"/cobros?socioId={id}")];

        var admin = await RenderAsync<TarjetaAcciones>(
            PermisosTestAuthHandler.Principal("Administrador"),
            new Dictionary<string, object?> { ["Id"] = "C1", ["Rapidas"] = rapidas, ["Resto"] = resto });
        var ejecutor = await RenderAsync<TarjetaAcciones>(
            PermisosTestAuthHandler.Principal("Ejecutor"),
            new Dictionary<string, object?> { ["Id"] = "C1", ["Rapidas"] = rapidas, ["Resto"] = resto });

        var menuAdmin = admin[admin.IndexOf("<details", StringComparison.Ordinal)..];
        Assert.DoesNotContain("href=\"/clientes/C1/editar\"", menuAdmin);
        Assert.Contains("href=\"/facturas-venta/nueva?socioId=C1\"", menuAdmin);
        Assert.Contains("href=\"/cobros?socioId=C1\"", menuAdmin);
        Assert.Contains("aria-label=\"Más acciones\"", admin);
        Assert.DoesNotContain("href=\"/clientes/C1/editar\"", ejecutor);
        Assert.Contains("href=\"/facturas-venta/nueva?socioId=C1\"", ejecutor[..ejecutor.IndexOf("<details", StringComparison.Ordinal)]);
    }

    [Fact]
    public async Task SeleccionFila_Marcada_YNoMarcada()
    {
        var marcada = await RenderAsync<SeleccionFila>(
            PermisosTestAuthHandler.Principal("Administrador"),
            new Dictionary<string, object?> { ["Href"] = "/x?sel=1", ["Seleccionada"] = true, ["Etiqueta"] = "KG" });
        var libre = await RenderAsync<SeleccionFila>(
            PermisosTestAuthHandler.Principal("Administrador"),
            new Dictionary<string, object?> { ["Href"] = "/x?sel=2", ["Seleccionada"] = false, ["Etiqueta"] = "UND" });

        Assert.Contains("aria-current=\"true\"", marcada);
        Assert.Contains("aria-label=\"Seleccionar KG\"", marcada);
        Assert.DoesNotContain("aria-current", libre);
    }

    [Fact]
    public async Task SeleccionFila_EsUnEnlaceEstirado_QueCubreLaFila()
    {
        var html = await RenderAsync<SeleccionFila>(
            PermisosTestAuthHandler.Principal("Administrador"),
            new Dictionary<string, object?> { ["Href"] = "/x?sel=1", ["Etiqueta"] = "KG" });

        // R13: el enlace de la primera celda se estira con ::after sobre la fila (que lleva SeleccionFila.ClaseFila = relative).
        Assert.Contains("after:absolute", html);
        Assert.Contains("after:inset-0", html);
        Assert.Contains("relative", SeleccionFila.ClaseFila);
        Assert.Contains("z-10", SeleccionFila.ClaseEncima);
    }

    [Theory]
    [InlineData("Administrador", true, true, true)]
    [InlineData("Supervisor", false, true, false)]
    [InlineData("Ejecutor", true, false, false)]
    public async Task PermisosPagina_CargaLosTresPermisosDelUsuario(string rol, bool add, bool modify, bool delete)
    {
        var permisos = await CargarPermisosAsync(PermisosTestAuthHandler.Principal(rol));

        Assert.Equal(add, permisos.CanAdd);
        Assert.Equal(modify, permisos.CanModify);
        Assert.Equal(delete, permisos.CanDelete);
    }

    [Fact]
    public async Task PermisosPagina_SinPermisos_OSinEstado_NoPermiteNada()
    {
        var sinPermisos = await CargarPermisosAsync(PermisosTestAuthHandler.Principal("Administrador", permisos: ""));
        var servicios = Servicios();
        var sinEstado = await PermisosPagina.CargarAsync(servicios.GetRequiredService<IAuthorizationService>(), null);

        Assert.False(sinPermisos.CanAdd || sinPermisos.CanModify || sinPermisos.CanDelete);
        Assert.False(sinEstado.CanAdd || sinEstado.CanModify || sinEstado.CanDelete);
        Assert.True(await sinPermisos.PuedeAsync(null));
        Assert.False(await sinPermisos.PuedeAsync(ApplicationPolicies.CanConsult));
    }

    [Fact]
    public async Task Toolbar_UsaLosPermisosRecibidos_SinVolverACargarlos()
    {
        // La página que ya cargó PermisosPagina la pasa a la barra: manda sobre el usuario en cascada.
        var supervisor = await CargarPermisosAsync(PermisosTestAuthHandler.Principal("Supervisor"));
        var html = await RenderAsync<PageToolbar>(
            PermisosTestAuthHandler.Principal("Administrador"),
            new Dictionary<string, object?>
            {
                ["Titulo"] = "T",
                ["NuevoHref"] = "/x/nuevo",
                ["SeleccionId"] = "abc",
                ["EditarUrl"] = Editar,
                ["EliminarUrl"] = Eliminar,
                ["Permisos"] = supervisor,
            });

        Assert.DoesNotContain("accion-nuevo", html);
        Assert.Contains("accion-editar", html);
        Assert.DoesNotContain("accion-eliminar", html);
    }

    private static ServiceProvider Servicios()
    {
        var coleccion = new ServiceCollection();
        coleccion.AddLogging();
        coleccion.AddAuthorizationCore(BlazorApp::OpenSource1.Blazor.Security.PoliticasBlazor.Configurar);
        return coleccion.BuildServiceProvider();
    }

    private static async Task<PermisosPagina> CargarPermisosAsync(ClaimsPrincipal usuario)
    {
        await using var servicios = Servicios();
        return await PermisosPagina.CargarAsync(
            servicios.GetRequiredService<IAuthorizationService>(), Task.FromResult(new AuthenticationState(usuario)));
    }

    /// <summary>R1: las aserciones se hacen sobre HTML decodificado (acentos de SinSeleccion, "Más acciones"…).</summary>
    private static async Task<string> RenderAsync<TComponente>(ClaimsPrincipal usuario, IDictionary<string, object?> parametros)
        where TComponente : Microsoft.AspNetCore.Components.IComponent =>
        HtmlSsr.Decodificar(await RenderizadorComponentes.RenderizarAsync<TComponente>(usuario, parametros));

    private static Task<string> ToolbarAsync(string rol, string? seleccion, IReadOnlyList<AccionPagina>? crear = null, IReadOnlyList<AccionPagina>? ver = null) =>
        RenderAsync<PageToolbar>(
            PermisosTestAuthHandler.Principal(rol),
            new Dictionary<string, object?>
            {
                ["Titulo"] = "Unidades de Medida",
                ["NuevoHref"] = "/x/nuevo",
                ["SeleccionId"] = seleccion,
                ["EditarUrl"] = Editar,
                ["EliminarUrl"] = Eliminar,
                ["Crear"] = crear ?? [],
                ["Ver"] = ver ?? [],
            });
}
