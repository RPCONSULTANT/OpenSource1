using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using OpenSource1.Application.Security;

namespace OpenSource1.Infrastructure.Identity;

/// <summary>
/// Resuelve políticas de autorización al vuelo para cualquier nombre con el prefijo
/// <see cref="Permisos.PolicyPrefix"/> (p.ej. <c>"permiso:maestros.socio.consultar"</c>),
/// evaluadas contra el claim <c>"permission"</c> del JWT vía <see cref="PermissionRequirement"/>
/// / <see cref="PermissionAuthorizationHandler"/>.
/// </summary>
/// <remarks>
/// ASP.NET Core solo admite un único <see cref="IAuthorizationPolicyProvider"/> registrado, así
/// que este provider envuelve un <see cref="DefaultAuthorizationPolicyProvider"/> de respaldo y
/// le delega cualquier nombre que no empiece por el prefijo de permisos — en particular, las 4
/// políticas coarse existentes (<see cref="ApplicationPolicies.CanAdd"/>,
/// <see cref="ApplicationPolicies.CanModify"/>, <see cref="ApplicationPolicies.CanDelete"/>,
/// <see cref="ApplicationPolicies.CanConsult"/>), que siguen registradas explícitamente con
/// <c>AddPolicy</c> en <c>DependencyInjection.AddApplicationIdentity</c>. Sin este respaldo,
/// registrar este provider dejaría de resolver esas 4 políticas.
/// </remarks>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallbackPolicyProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallbackPolicyProvider.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Permisos.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var permiso = policyName[Permisos.PolicyPrefix.Length..];
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(permiso))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallbackPolicyProvider.GetPolicyAsync(policyName);
    }
}
