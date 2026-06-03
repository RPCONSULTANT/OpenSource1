using OpenSource1.Application.Services.Auth.Dtos;

namespace OpenSource1.Blazor.Services;

public interface IAuthApiClient
{
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}

public sealed record AuthResult(AuthResponse? Response, IReadOnlyList<string> Errors)
{
    public bool Succeeded => Response is not null;
}
