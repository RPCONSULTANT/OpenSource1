using Microsoft.AspNetCore.Authorization;

namespace OpenSource1.Infrastructure.Identity;

/// <summary>
/// Requisito de autorización para un permiso fino del catálogo (<see cref="Permisos"/>), p.ej.
/// <c>"maestros.socio.consultar"</c>. Lo crea <see cref="PermissionPolicyProvider"/> al resolver
/// una política nombrada <c>"permiso:&lt;permiso&gt;"</c>.
/// </summary>
public sealed class PermissionRequirement(string permiso) : IAuthorizationRequirement
{
    public string Permiso { get; } = permiso;
}

/// <summary>
/// Evalúa un <see cref="PermissionRequirement"/> contra los claims <c>"permission"</c> emitidos
/// por <c>AuthService</c> — el mismo claim type que usan hoy las 4 políticas coarse
/// (<see cref="OpenSource1.Application.Security.ApplicationPolicies"/>) y que consume
/// Blazor (Login.razor / Program.cs) sin cambios.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var tienePermiso = context.User.Claims.Any(claim =>
            claim.Type == "permission" &&
            string.Equals(claim.Value, requirement.Permiso, StringComparison.OrdinalIgnoreCase));

        if (tienePermiso)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
