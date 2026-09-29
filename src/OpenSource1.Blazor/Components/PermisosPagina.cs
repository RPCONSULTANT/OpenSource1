using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using OpenSource1.Application.Security;

namespace OpenSource1.Blazor.Components;

/// <summary>
/// Permisos coarse de una página (Fix-Features B1, ruling R6): CanAdd / CanModify / CanDelete evaluados UNA vez con
/// <see cref="IAuthorizationService"/> y el usuario del <see cref="AuthenticationState"/> en cascada. Lo usan
/// <see cref="PageToolbar"/> (o se le pasa ya cargado en su parámetro <c>Permisos</c>) y los editores de B2/C para decidir
/// si muestran el formulario o "No tiene permiso para realizar esta acción.". Sin estado de autenticación no permite nada.
/// </summary>
public sealed class PermisosPagina
{
    private readonly IAuthorizationService autorizacion;

    private PermisosPagina(IAuthorizationService autorizacion, ClaimsPrincipal usuario, bool canAdd, bool canModify, bool canDelete)
    {
        this.autorizacion = autorizacion;
        Usuario = usuario;
        CanAdd = canAdd;
        CanModify = canModify;
        CanDelete = canDelete;
    }

    public ClaimsPrincipal Usuario { get; }
    public bool CanAdd { get; }
    public bool CanModify { get; }
    public bool CanDelete { get; }

    public static async Task<PermisosPagina> CargarAsync(IAuthorizationService autorizacion, Task<AuthenticationState>? estado)
    {
        var usuario = estado is null ? new ClaimsPrincipal() : (await estado).User;
        return await DeUsuarioAsync(autorizacion, usuario);
    }

    /// <summary>Variante con el usuario ya resuelto (p. ej. cuando la página ya esperó el AuthenticationState).</summary>
    public static async Task<PermisosPagina> DeUsuarioAsync(IAuthorizationService autorizacion, ClaimsPrincipal usuario) =>
        new(
            autorizacion,
            usuario,
            await AccionesPermitidas.PuedeAsync(autorizacion, usuario, ApplicationPolicies.CanAdd),
            await AccionesPermitidas.PuedeAsync(autorizacion, usuario, ApplicationPolicies.CanModify),
            await AccionesPermitidas.PuedeAsync(autorizacion, usuario, ApplicationPolicies.CanDelete));

    /// <summary>
    /// ¿Puede el usuario la política <paramref name="permiso"/>? null = sin restricción; las tres políticas cargadas se
    /// responden sin volver a evaluar; cualquier otra (CanConsult, CanAdministrar…) se evalúa contra el mismo usuario.
    /// </summary>
    public Task<bool> PuedeAsync(string? permiso) => permiso switch
    {
        null => Task.FromResult(true),
        ApplicationPolicies.CanAdd => Task.FromResult(CanAdd),
        ApplicationPolicies.CanModify => Task.FromResult(CanModify),
        ApplicationPolicies.CanDelete => Task.FromResult(CanDelete),
        _ => AccionesPermitidas.PuedeAsync(autorizacion, Usuario, permiso),
    };

    /// <summary>Filtra <paramref name="acciones"/> por su <see cref="AccionPagina.Permiso"/> (sin permiso no se muestran).</summary>
    public async Task<IReadOnlyList<AccionPagina>> FiltrarAsync(IEnumerable<AccionPagina> acciones)
    {
        var permitidas = new List<AccionPagina>();
        foreach (var accion in acciones)
        {
            if (await PuedeAsync(accion.Permiso))
            {
                permitidas.Add(accion);
            }
        }

        return permitidas;
    }
}
