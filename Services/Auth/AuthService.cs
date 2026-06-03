using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using test.Identity;
using test.Security;
using test.Services.Auth.Dtos;

namespace test.Services.Auth;

public sealed class AuthService(
    UserManager<Usuario> userManager,
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

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        var passwordIsValid = await userManager.CheckPasswordAsync(user, request.Password);

        return passwordIsValid ? await CreateAuthResponseAsync(user) : null;
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(Usuario user)
    {
        var expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);
        var roles = await userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

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
            accessToken,
            expiresAtUtc);
    }
}
