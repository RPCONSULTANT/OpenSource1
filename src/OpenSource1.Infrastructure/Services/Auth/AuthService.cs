using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenSource1.Infrastructure.Identity;
using OpenSource1.Application.Security;
using OpenSource1.Application.Services.Auth;
using OpenSource1.Application.Services.Auth.Dtos;

namespace OpenSource1.Infrastructure.Services.Auth;

public sealed class AuthService(
    UserManager<Usuario> userManager,
    SignInManager<Usuario> signInManager,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<(AuthResponse? Response, IReadOnlyList<string> Errors)> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = new Usuario
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            EmailConfirmed = true,
            IsActive = true
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return (null, result.Errors.Select(error => error.Description).ToArray());
        }

        return (await CreateAuthResponseAsync(user), []);
    }

    public async Task<(AuthResponse? Response, IReadOnlyList<string> Errors)> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserNameOrEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Password);

        var user = request.UserNameOrEmail.Contains('@', StringComparison.Ordinal)
            ? await userManager.FindByEmailAsync(request.UserNameOrEmail)
            : await userManager.FindByNameAsync(request.UserNameOrEmail);

        if (user is null || !user.IsActive)
        {
            return (null, ["Usuario o contraseña inválidos."]);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return (null, ["Usuario bloqueado temporalmente por intentos fallidos."]);
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return (null, ["Usuario bloqueado temporalmente por intentos fallidos."]);
        }

        if (!result.Succeeded)
        {
            return (null, ["Usuario o contraseña inválidos."]);
        }

        return (await CreateAuthResponseAsync(user), []);
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(Usuario user)
    {
        var expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);
        var roles = (await userManager.GetRolesAsync(user)).ToArray();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var permissions = GetPermissions(roles);
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new AuthResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName ?? string.Empty,
            roles,
            permissions,
            accessToken,
            expiresAtUtc);
    }

    private static string[] GetPermissions(IEnumerable<string> roles)
    {
        var roleSet = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (roleSet.Contains(ApplicationRoles.Administrator))
        {
            permissions.UnionWith([ApplicationPolicies.CanAdd, ApplicationPolicies.CanModify, ApplicationPolicies.CanDelete, ApplicationPolicies.CanConsult]);
        }

        if (roleSet.Contains(ApplicationRoles.Supervisor))
        {
            permissions.UnionWith([ApplicationPolicies.CanModify, ApplicationPolicies.CanConsult]);
        }

        if (roleSet.Contains(ApplicationRoles.Executor))
        {
            permissions.UnionWith([ApplicationPolicies.CanAdd, ApplicationPolicies.CanConsult]);
        }

        return permissions.Order().ToArray();
    }
}
