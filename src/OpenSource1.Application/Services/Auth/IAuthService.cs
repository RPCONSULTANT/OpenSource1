using OpenSource1.Application.Services.Auth.Dtos;

namespace OpenSource1.Application.Services.Auth;

public interface IAuthService
{
    Task<(AuthResponse? Response, IReadOnlyList<string> Errors)> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<(AuthResponse? Response, IReadOnlyList<string> Errors)> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
