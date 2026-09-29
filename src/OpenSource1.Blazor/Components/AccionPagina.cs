using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace OpenSource1.Blazor.Components;

/// <summary>
/// Acción de <see cref="PageToolbar"/> o de <see cref="TarjetaAcciones"/> (Fix-Features B1). <c>UrlConId</c> recibe el id
/// seleccionado (null sin selección). <c>Permiso</c> es una política (ApplicationPolicies.*): sin permiso la acción no se
/// muestra. <c>RequiereSeleccion</c>: sin selección se muestra deshabilitada con <see cref="PageToolbar.SinSeleccion"/>.
/// </summary>
public sealed record AccionPagina(
    string Etiqueta, string Icono, Func<string?, string> UrlConId, string? Permiso = null, bool RequiereSeleccion = false);

public sealed record Miga(string Texto, string? Href = null);

/// <summary>Evaluación de una política suelta; para filtrar listas de acciones usa <see cref="PermisosPagina.FiltrarAsync"/>.</summary>
public static class AccionesPermitidas
{
    public static async Task<bool> PuedeAsync(IAuthorizationService autorizacion, ClaimsPrincipal usuario, string? permiso) =>
        permiso is null || (await autorizacion.AuthorizeAsync(usuario, permiso)).Succeeded;
}
