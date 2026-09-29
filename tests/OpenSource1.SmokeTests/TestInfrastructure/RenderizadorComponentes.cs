extern alias BlazorApp;

using System.Security.Claims;
using BlazorApp::OpenSource1.Blazor.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OpenSource1.SmokeTests.TestInfrastructure;

/// <summary>
/// Renderiza un componente a HTML con <see cref="HtmlRenderer"/> (sin host): servicios mínimos (logging y las políticas
/// reales de <see cref="PoliticasBlazor"/>) y el <c>Task&lt;AuthenticationState&gt;</c> en cascada con el usuario dado.
/// Para componentes sin NavigationManager (barras, tarjetas, páginas-tarjeta).
/// </summary>
public static class RenderizadorComponentes
{
    public static async Task<string> RenderizarAsync<TComponente>(
        ClaimsPrincipal usuario, IDictionary<string, object?> parametros, Action<IServiceCollection>? servicios = null)
        where TComponente : IComponent
    {
        var coleccion = new ServiceCollection();
        coleccion.AddLogging();
        coleccion.AddAuthorizationCore(PoliticasBlazor.Configurar);
        servicios?.Invoke(coleccion);

        await using var proveedor = coleccion.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(proveedor, proveedor.GetRequiredService<ILoggerFactory>());
        var estado = Task.FromResult(new AuthenticationState(usuario));

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            RenderFragment hijo = builder =>
            {
                builder.OpenComponent<TComponente>(0);
                // Número de secuencia literal (ASP0006): en un bucle se repite, como en el código generado por Razor.
                foreach (var (clave, valor) in parametros)
                {
                    builder.AddComponentParameter(1, clave, valor);
                }

                builder.CloseComponent();
            };

            var salida = await renderer.RenderComponentAsync<CascadingValue<Task<AuthenticationState>>>(
                ParameterView.FromDictionary(new Dictionary<string, object?> { ["Value"] = estado, ["ChildContent"] = hijo }));
            return salida.ToHtmlString();
        });
    }
}
