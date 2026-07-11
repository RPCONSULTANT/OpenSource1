using OpenSource1.Application.Services.Auth.Dtos;

namespace OpenSource1.Blazor.Services;

public interface IAuthApiClient
{
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<PasswordActionResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
    Task<PasswordActionResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
    Task<PasswordActionResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task<CurrentUserResponse?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
    Task<(bool Success, IReadOnlyList<string> Errors)> UpdateProfileImageAsync(UpdateProfileImageRequest request, CancellationToken cancellationToken = default);
}

public sealed record AuthResult(AuthResponse? Response, IReadOnlyList<string> Errors)
{
    public bool Succeeded => Response is not null;
}

public sealed record PasswordActionResult(PasswordActionResponse? Response, IReadOnlyList<string> Errors)
{
    public bool Succeeded => Response is not null && Errors.Count == 0;
}
