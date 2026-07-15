using OpenSource1.Application.Features.Users.Dtos;

namespace OpenSource1.Blazor.Services;

public interface IUserAdminApiClient
{
    Task<(IReadOnlyList<UserSummaryResponse>? Users, string? Error)> ListUsersAsync(
        string? search = null, string? role = null, bool? isActive = null, CancellationToken cancellationToken = default);
    Task<(UserSummaryResponse? User, string? Error)> GetByIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> CreateAsync(string email, string fullName, string password, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> UpdateAsync(string userId, string fullName, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> ResetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> DeleteAsync(string userId, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> AssignRoleAsync(string userId, string role, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> RemoveRoleAsync(string userId, string role, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> ToggleActiveAsync(string userId, CancellationToken cancellationToken = default);
}
